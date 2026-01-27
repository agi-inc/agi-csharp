using Agi.Types;
using Agi.Resources;

namespace Agi;

/// <summary>
/// State of the agent loop
/// </summary>
public enum LoopState
{
    Idle,
    Running,
    Paused,
    Finished,
    Error
}

/// <summary>
/// Options for creating an AgentLoop
/// </summary>
public class AgentLoopOptions
{
    /// <summary>
    /// AGI client instance
    /// </summary>
    public AgiClient Client { get; set; } = null!;

    /// <summary>
    /// Agent URL from the session response
    /// </summary>
    public string AgentUrl { get; set; } = null!;

    /// <summary>
    /// Session ID for routing
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Function to capture a screenshot (returns base64-encoded image)
    /// </summary>
    public Func<Task<string>> CaptureScreenshot { get; set; } = null!;

    /// <summary>
    /// Function to execute actions on the local environment
    /// </summary>
    public Func<IReadOnlyList<DesktopAction>, Task> ExecuteActions { get; set; } = null!;

    /// <summary>
    /// Callback when the agent is thinking
    /// </summary>
    public Action<string>? OnThinking { get; set; }

    /// <summary>
    /// Callback to get user input when the agent asks a question
    /// </summary>
    public Func<string, Task<string>>? OnAskUser { get; set; }

    /// <summary>
    /// Callback on each step
    /// </summary>
    public Action<int, StepDesktopResponse>? OnStep { get; set; }

    /// <summary>
    /// Callback when an error occurs
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// Delay between steps in milliseconds
    /// </summary>
    public int StepDelayMs { get; set; } = 500;

    /// <summary>
    /// Maximum number of steps (0 = unlimited)
    /// </summary>
    public int MaxSteps { get; set; } = 0;
}

/// <summary>
/// Manages the desktop mode execution loop
/// </summary>
public class AgentLoop
{
    private readonly AgiClient _client;
    private readonly string _agentUrl;
    private readonly string _sessionId;
    private readonly Func<Task<string>> _captureScreenshot;
    private readonly Func<IReadOnlyList<DesktopAction>, Task> _executeActions;
    private readonly Action<string>? _onThinking;
    private readonly Func<string, Task<string>>? _onAskUser;
    private readonly Action<int, StepDesktopResponse>? _onStep;
    private readonly Action<Exception>? _onError;
    private readonly int _stepDelayMs;
    private readonly int _maxSteps;

    private LoopState _state = LoopState.Idle;
    private int _currentStep = 0;
    private StepDesktopResponse? _lastResult;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _pauseTcs;

    /// <summary>
    /// Current state of the loop
    /// </summary>
    public LoopState State => _state;

    /// <summary>
    /// Current step number
    /// </summary>
    public int CurrentStep => _currentStep;

    /// <summary>
    /// Last result from the agent
    /// </summary>
    public StepDesktopResponse? LastResult => _lastResult;

    /// <summary>
    /// Whether the loop is running
    /// </summary>
    public bool IsRunning => _state == LoopState.Running;

    /// <summary>
    /// Whether the loop is paused
    /// </summary>
    public bool IsPaused => _state == LoopState.Paused;

    /// <summary>
    /// Whether the loop has finished
    /// </summary>
    public bool IsFinished => _state == LoopState.Finished;

    /// <summary>
    /// Create a new agent loop
    /// </summary>
    public AgentLoop(AgentLoopOptions options)
    {
        _client = options.Client ?? throw new ArgumentNullException(nameof(options), "Client is required");
        _agentUrl = options.AgentUrl ?? throw new ArgumentNullException(nameof(options), "AgentUrl is required");
        _sessionId = options.SessionId ?? throw new ArgumentNullException(nameof(options), "SessionId is required");
        _captureScreenshot = options.CaptureScreenshot ?? throw new ArgumentNullException(nameof(options), "CaptureScreenshot is required");
        _executeActions = options.ExecuteActions ?? throw new ArgumentNullException(nameof(options), "ExecuteActions is required");
        _onThinking = options.OnThinking;
        _onAskUser = options.OnAskUser;
        _onStep = options.OnStep;
        _onError = options.OnError;
        _stepDelayMs = options.StepDelayMs;
        _maxSteps = options.MaxSteps;
    }

    /// <summary>
    /// Start the agent loop
    /// </summary>
    /// <param name="message">Optional initial message/task description</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final result from the agent</returns>
    public async Task<StepDesktopResponse> StartAsync(
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        if (_state == LoopState.Running)
        {
            throw new InvalidOperationException("Agent loop is already running");
        }

        _state = LoopState.Running;
        _currentStep = 0;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            string? currentMessage = message;
            string? userResponse = null;

            while (!_cts.Token.IsCancellationRequested)
            {
                // Check if paused
                if (_pauseTcs != null)
                {
                    _state = LoopState.Paused;
                    await _pauseTcs.Task;
                    _state = LoopState.Running;
                    _pauseTcs = null;
                }

                // Check max steps
                if (_maxSteps > 0 && _currentStep >= _maxSteps)
                {
                    throw new AgentExecutionException(
                        $"Max steps ({_maxSteps}) exceeded",
                        _sessionId,
                        _currentStep);
                }

                // Capture screenshot
                var screenshot = await _captureScreenshot();

                // Call step_desktop
                _currentStep++;
                _lastResult = await _client.Sessions.StepAsync(
                    _agentUrl,
                    _sessionId,
                    screenshot,
                    currentMessage,
                    userResponse,
                    _cts.Token);

                // Clear message after first step
                currentMessage = null;
                userResponse = null;

                // Invoke callbacks
                if (_lastResult.Thinking != null)
                {
                    _onThinking?.Invoke(_lastResult.Thinking);
                }

                _onStep?.Invoke(_currentStep, _lastResult);

                // Check if finished
                if (_lastResult.Finished)
                {
                    _state = LoopState.Finished;
                    return _lastResult;
                }

                // Check if asking for user input
                if (_lastResult.AskUser != null)
                {
                    if (_onAskUser == null)
                    {
                        throw new AgentExecutionException(
                            $"Agent asked for user input but no OnAskUser handler provided: {_lastResult.AskUser}",
                            _sessionId,
                            _currentStep);
                    }

                    userResponse = await _onAskUser(_lastResult.AskUser);
                    continue; // Skip action execution, send user response
                }

                // Execute actions
                if (_lastResult.Actions.Count > 0)
                {
                    await _executeActions(_lastResult.Actions);
                }

                // Delay before next step
                if (_stepDelayMs > 0)
                {
                    await Task.Delay(_stepDelayMs, _cts.Token);
                }
            }

            _cts.Token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Loop exited unexpectedly");
        }
        catch (OperationCanceledException)
        {
            _state = LoopState.Idle;
            throw;
        }
        catch (Exception ex)
        {
            _state = LoopState.Error;
            _onError?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// Pause the agent loop
    /// </summary>
    public void Pause()
    {
        if (_state != LoopState.Running)
            return;

        _pauseTcs = new TaskCompletionSource<bool>();
    }

    /// <summary>
    /// Resume a paused agent loop
    /// </summary>
    public void Resume()
    {
        if (_state != LoopState.Paused)
            return;

        _pauseTcs?.TrySetResult(true);
    }

    /// <summary>
    /// Stop the agent loop
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _pauseTcs?.TrySetCanceled();
    }
}

/// <summary>
/// Extension methods for creating AgentLoop from a session
/// </summary>
public static class AgentLoopExtensions
{
    /// <summary>
    /// Create an agent loop from a session context
    /// </summary>
    public static AgentLoop CreateAgentLoop(
        this Agi.Context.SessionContext session,
        AgiClient client,
        Func<Task<string>> captureScreenshot,
        Func<IReadOnlyList<DesktopAction>, Task> executeActions,
        Action<AgentLoopOptions>? configure = null)
    {
        if (session.AgentUrl == null)
        {
            throw new InvalidOperationException(
                "Session does not have an agent URL. Make sure the session was created with AgentSessionType.Desktop");
        }

        var options = new AgentLoopOptions
        {
            Client = client,
            AgentUrl = session.AgentUrl,
            SessionId = session.SessionId,
            CaptureScreenshot = captureScreenshot,
            ExecuteActions = executeActions
        };

        configure?.Invoke(options);

        return new AgentLoop(options);
    }
}
