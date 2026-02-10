using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Agi.Driver;

/// <summary>
/// Options for creating an AgentDriver.
/// </summary>
public class DriverOptions
{
    /// <summary>
    /// Path to the agi-driver binary. If not provided, will be auto-detected.
    /// </summary>
    public string? BinaryPath { get; set; }

    /// <summary>
    /// Model to use (default: "claude-sonnet").
    /// </summary>
    public string Model { get; set; } = "claude-sonnet";

    /// <summary>
    /// Platform type (default: "desktop").
    /// </summary>
    public string Platform { get; set; } = "desktop";

    /// <summary>
    /// "local" for autonomous mode, "remote" for managed VM, "" for legacy SDK-driven mode.
    /// </summary>
    public string Mode { get; set; } = "";

    /// <summary>
    /// Agent name for the AGI API (e.g., "agi-2-claude").
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// AGI API base URL (default: "https://api.agi.tech").
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Environment type for remote mode ("ubuntu-1" or "chrome-1").
    /// </summary>
    public string? EnvironmentType { get; set; }

    /// <summary>
    /// Environment variables to pass to the driver process.
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    // Multimodal options

    /// <summary>
    /// Enable voice input/output.
    /// </summary>
    public bool Voice { get; set; }

    /// <summary>
    /// Enable camera video feed.
    /// </summary>
    public bool Camera { get; set; }

    /// <summary>
    /// Enable screen recording.
    /// </summary>
    public bool Screen { get; set; }

    /// <summary>
    /// Enable MCP servers.
    /// </summary>
    public bool Mcp { get; set; }

    /// <summary>
    /// Path to MCP config file.
    /// </summary>
    public string McpConfig { get; set; } = "~/.agi/mcp.json";
}

/// <summary>
/// Result from running the driver.
/// </summary>
public class DriverResult
{
    /// <summary>
    /// Whether the task completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Reason for completion.
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// Summary of what was accomplished.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Final step number.
    /// </summary>
    public int Step { get; set; }
}

/// <summary>
/// AgentDriver manages the lifecycle of the agi-driver binary.
/// </summary>
public class AgentDriver : IDisposable, IAsyncDisposable
{
    private readonly string _binaryPath;
    private readonly string _model;
    private readonly string _platform;
    private readonly string _mode;
    private readonly string? _agentName;
    private readonly string? _apiUrl;
    private readonly string? _environmentType;
    private readonly Dictionary<string, string>? _env;
    private readonly bool _voice;
    private readonly bool _camera;
    private readonly bool _screen;
    private readonly bool _mcp;
    private readonly string _mcpConfig;

    private Process? _process;
    private StreamWriter? _stdin;
    private DriverState _state = DriverState.Idle;
    private int _step;
    private string _sessionId = "";
    private TaskCompletionSource<DriverResult>? _resultTcs;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    // Event handlers
    public event Func<string, Task>? OnThinking;
    public event Func<DriverAction, Task>? OnAction;
    public event Func<string, Task<bool>>? OnConfirm;
    public event Func<string, Task<string>>? OnAskQuestion;
    public event Action<DriverState>? OnStateChange;
    public event Action<BaseDriverEvent>? OnEvent;
    public event Action<string>? OnError;

    /// <summary>
    /// Create a new AgentDriver.
    /// </summary>
    public AgentDriver(DriverOptions? options = null)
    {
        var opts = options ?? new DriverOptions();

        // Find the native binary
        _binaryPath = opts.BinaryPath ?? BinaryLocator.FindBinaryPath();
        _model = opts.Model;
        _platform = opts.Platform;
        _mode = opts.Mode;
        _agentName = opts.AgentName;
        _apiUrl = opts.ApiUrl;
        _environmentType = opts.EnvironmentType;
        _env = opts.Environment;
        _voice = opts.Voice;
        _camera = opts.Camera;
        _screen = opts.Screen;
        _mcp = opts.Mcp;
        _mcpConfig = opts.McpConfig;
    }

    /// <summary>
    /// Load MCP server configuration from a JSON file.
    /// </summary>
    private static List<MCPServerConfig>? LoadMcpConfig(string configPath)
    {
        try
        {
            var expanded = configPath.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), configPath[2..])
                : Path.GetFullPath(configPath);

            if (!File.Exists(expanded))
                return null;

            var json = File.ReadAllText(expanded);
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (config == null) return null;

            var servers = new List<MCPServerConfig>();
            foreach (var (name, serverElement) in config)
            {
                var server = new MCPServerConfig
                {
                    Name = name,
                    Command = serverElement.TryGetProperty("command", out var cmd) ? cmd.GetString() ?? "" : "",
                    Args = serverElement.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array
                        ? args.EnumerateArray().Select(a => a.GetString() ?? "").ToArray()
                        : Array.Empty<string>(),
                };
                if (serverElement.TryGetProperty("env", out var env) && env.ValueKind == JsonValueKind.Object)
                {
                    server.Env = new Dictionary<string, string>();
                    foreach (var prop in env.EnumerateObject())
                    {
                        server.Env[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }
                servers.Add(server);
            }
            return servers;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the current state.
    /// </summary>
    public DriverState State => _state;

    /// <summary>
    /// Get the current step number.
    /// </summary>
    public int Step => _step;

    /// <summary>
    /// Check if the driver is running.
    /// </summary>
    public bool IsRunning => _state == DriverState.Running;

    /// <summary>
    /// Check if the driver is waiting for user input.
    /// </summary>
    public bool IsWaiting => _state == DriverState.WaitingConfirmation || _state == DriverState.WaitingAnswer;

    /// <summary>
    /// Start the agent with a goal.
    /// </summary>
    /// <param name="goal">The task for the agent to accomplish.</param>
    /// <param name="screenshot">Initial screenshot (base64-encoded). Not needed in local mode.</param>
    /// <param name="screenWidth">Screen width in pixels. Not needed in local mode.</param>
    /// <param name="screenHeight">Screen height in pixels. Not needed in local mode.</param>
    /// <param name="mode">Override the mode set in DriverOptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DriverResult> StartAsync(
        string goal,
        string screenshot = "",
        int screenWidth = 0,
        int screenHeight = 0,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        if (_process != null)
            throw new InvalidOperationException("Driver is already running");

        _sessionId = $"session_{Guid.NewGuid():N}";
        _resultTcs = new TaskCompletionSource<DriverResult>();
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start the process
        var startInfo = new ProcessStartInfo
        {
            FileName = _binaryPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (_env != null)
        {
            foreach (var (key, value) in _env)
            {
                startInfo.Environment[key] = value;
            }
        }

        _process = new Process { StartInfo = startInfo };
        _process.Start();
        _stdin = _process.StandardInput;

        // Start reading stdout
        _readTask = ReadLoopAsync(_readCts.Token);

        // Wait for ready event
        var readyTcs = new TaskCompletionSource<bool>();
        void OnReady(BaseDriverEvent evt)
        {
            if (evt is ReadyEvent)
                readyTcs.TrySetResult(true);
        }

        OnEvent += OnReady;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            try
            {
                await readyTcs.Task.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                await StopAsync();
                throw new TimeoutException("Driver failed to start: timeout waiting for ready event");
            }
        }
        finally
        {
            OnEvent -= OnReady;
        }

        // Send start command
        var startCmd = new StartCommand
        {
            SessionId = _sessionId,
            Goal = goal,
            Screenshot = screenshot,
            ScreenWidth = screenWidth,
            ScreenHeight = screenHeight,
            Platform = _platform,
            Model = _model,
            Mode = mode ?? _mode,
            AgentName = _agentName,
            ApiUrl = _apiUrl,
            EnvironmentType = _environmentType,
            // Multimodal options
            AudioInputEnabled = _voice ? true : null,
            TurnDetectionEnabled = _voice ? true : null,
            SpeechOutputEnabled = _voice ? true : null,
            SpeechVoice = _voice ? "alloy" : null,
            CameraEnabled = _camera ? true : null,
            ScreenRecordingEnabled = _screen ? true : null,
            McpServers = _mcp ? LoadMcpConfig(_mcpConfig) : null,
        };
        await SendCommandAsync(startCmd);

        // Wait for result
        return await _resultTcs.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Send a new screenshot to the driver.
    /// </summary>
    public async Task SendScreenshotAsync(string screenshot, int screenWidth = 0, int screenHeight = 0)
    {
        if (_process == null)
            throw new InvalidOperationException("Driver is not running");

        var cmd = new ScreenshotCommand
        {
            Data = screenshot,
            ScreenWidth = screenWidth,
            ScreenHeight = screenHeight
        };
        await SendCommandAsync(cmd);
    }

    /// <summary>
    /// Pause the driver.
    /// </summary>
    public async Task PauseAsync()
    {
        if (_process == null) return;
        await SendCommandAsync(new PauseCommand());
    }

    /// <summary>
    /// Resume the driver.
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_process == null) return;
        await SendCommandAsync(new ResumeCommand());
    }

    /// <summary>
    /// Stop the driver.
    /// </summary>
    public async Task StopAsync(string? reason = null)
    {
        if (_process == null) return;

        await SendCommandAsync(new StopCommand { Reason = reason });

        // Wait for process to exit
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _process.WaitForExitAsync(cts.Token);
        }
        catch
        {
            _process.Kill();
        }

        await CleanupAsync();
    }

    /// <summary>
    /// Respond to a confirmation request.
    /// </summary>
    public async Task RespondConfirmAsync(bool approved, string? message = null)
    {
        if (_process == null || _state != DriverState.WaitingConfirmation)
            throw new InvalidOperationException("Not waiting for confirmation");

        var cmd = new ConfirmResponseCommand
        {
            Approved = approved,
            Message = message
        };
        await SendCommandAsync(cmd);
    }

    /// <summary>
    /// Respond to a question.
    /// </summary>
    public async Task RespondAnswerAsync(string text, string? questionId = null)
    {
        if (_process == null || _state != DriverState.WaitingAnswer)
            throw new InvalidOperationException("Not waiting for answer");

        var cmd = new AnswerCommand
        {
            Text = text,
            QuestionId = questionId
        };
        await SendCommandAsync(cmd);
    }

    private async Task SendCommandAsync<T>(T command)
    {
        if (_stdin == null) return;
        var line = DriverProtocol.SerializeCommand(command);
        await _stdin.WriteLineAsync(line);
        await _stdin.FlushAsync();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_process == null) return;

        var reader = _process.StandardOutput;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                #if NET7_0_OR_GREATER
                var line = await reader.ReadLineAsync(cancellationToken);
#else
                var line = await ReadLineWithCancellationAsync(reader, cancellationToken);
#endif
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var evt = DriverProtocol.ParseEvent(line);
                    await HandleEventAsync(evt);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Error parsing event: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    private async Task HandleEventAsync(BaseDriverEvent evt)
    {
        _step = evt.Step;
        OnEvent?.Invoke(evt);

        switch (evt)
        {
            case StateChangeEvent stateEvent:
                _state = stateEvent.GetState();
                OnStateChange?.Invoke(_state);
                break;

            case ThinkingEvent thinkingEvent:
                if (OnThinking != null)
                    await OnThinking(thinkingEvent.Text);
                break;

            case ActionEvent actionEvent:
                if (OnAction != null)
                    await OnAction(actionEvent.Action);
                break;

            case ConfirmEvent confirmEvent:
                _state = DriverState.WaitingConfirmation;
                if (OnConfirm != null)
                {
                    try
                    {
                        var approved = await OnConfirm(confirmEvent.Reason);
                        await RespondConfirmAsync(approved);
                    }
                    catch
                    {
                        // Wait for manual response
                    }
                }
                break;

            case AskQuestionEvent questionEvent:
                _state = DriverState.WaitingAnswer;
                if (OnAskQuestion != null)
                {
                    try
                    {
                        var answer = await OnAskQuestion(questionEvent.Question);
                        await RespondAnswerAsync(answer, questionEvent.QuestionId);
                    }
                    catch
                    {
                        // Wait for manual response
                    }
                }
                break;

            case ScreenshotCapturedEvent:
                // Informational event, no action needed
                break;

            case SessionCreatedEvent:
                // Informational event, available via OnEvent handler
                break;

            case FinishedEvent finishedEvent:
                HandleFinished(finishedEvent);
                break;

            case ErrorEvent errorEvent:
                await HandleErrorAsync(errorEvent);
                break;
        }
    }

    private void HandleFinished(FinishedEvent evt)
    {
        _state = DriverState.Finished;
        var result = new DriverResult
        {
            Success = evt.Success,
            Reason = evt.Reason,
            Summary = evt.Summary,
            Step = evt.Step
        };
        _resultTcs?.TrySetResult(result);
        _ = CleanupAsync();
    }

    private async Task HandleErrorAsync(ErrorEvent evt)
    {
        OnError?.Invoke($"{evt.Code}: {evt.Message}");

        if (!evt.Recoverable)
        {
            _state = DriverState.Error;
            _resultTcs?.TrySetException(new Exception($"{evt.Code}: {evt.Message}"));
            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        _readCts?.Cancel();

        if (_readTask != null)
        {
            try
            {
                await _readTask;
            }
            catch
            {
                // Ignore
            }
            _readTask = null;
        }

        _stdin?.Dispose();
        _stdin = null;

        if (_process != null)
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                await _process.WaitForExitAsync();
            }
            _process.Dispose();
            _process = null;
        }

        _readCts?.Dispose();
        _readCts = null;
    }

    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }

#if !NET7_0_OR_GREATER
    /// <summary>
    /// Helper method to read a line with cancellation support for .NET 6.0.
    /// </summary>
    private static async Task<string?> ReadLineWithCancellationAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var readTask = reader.ReadLineAsync();
        var delayTask = Task.Delay(Timeout.Infinite, cancellationToken);
        
        var completedTask = await Task.WhenAny(readTask, delayTask);
        
        if (completedTask == delayTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        
        return await readTask;
    }
#endif
}
