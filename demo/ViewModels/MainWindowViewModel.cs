using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgiDemo.Services;

namespace AgiDemo.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AgiService _service = new();
    private CancellationTokenSource? _streamCts;
    private DispatcherTimer? _screenshotTimer;

    // --- Observable Properties ---

    [ObservableProperty]
    private string _apiKey = Environment.GetEnvironmentVariable("AGI_API_KEY") ?? string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _selectedAgent = "agi-0";

    [ObservableProperty]
    private SessionItemViewModel? _selectedSession;

    [ObservableProperty]
    private string _taskText = string.Empty;

    [ObservableProperty]
    private string _connectionStatusText = "\u26AA Disconnected";

    [ObservableProperty]
    private string _sessionStatusText = "Session: --";

    [ObservableProperty]
    private string _runStatusText = "Idle";

    [ObservableProperty]
    private string _runStatusColor = "#888888";

    [ObservableProperty]
    private byte[]? _screenshotData;

    [ObservableProperty]
    private bool _hasScreenshot;

    [ObservableProperty]
    private bool _canPause;

    [ObservableProperty]
    private bool _canResume;

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private bool _canSend;

    public ObservableCollection<string> AgentModels { get; } = new()
    {
        "agi-0", "agi-1", "agi-2-claude", "agi-2-gpt"
    };

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = new();
    public ObservableCollection<EventItemViewModel> Events { get; } = new();

    // --- Quick Card Tasks ---

    public static (string Label, string Task)[] QuickCards { get; } =
    {
        ("Flights", "Find 3 nonstop SFO to JFK flights under $450 round trip"),
        ("Uber", "Open Uber and request a ride from downtown to the airport"),
        ("Instacart", "Order 2 lbs of organic bananas and almond milk on Instacart"),
        ("Meeting", "Schedule a 30-min team standup for tomorrow at 10am on Google Calendar"),
    };

    // --- Commands ---

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return;

        try
        {
            _service.Connect(ApiKey);
            IsConnected = true;
            ConnectionStatusText = "\U0001F7E2 Connected";
            AddEvent("log", "Connected to AGI API");

            // Load models
            try
            {
                var models = await _service.ListModelsAsync("cdp");
                if (models.Count > 0)
                {
                    AgentModels.Clear();
                    foreach (var m in models)
                    {
                        var name = m.Name ?? m.ToString()!;
                        AgentModels.Add(name);
                    }
                    SelectedAgent = AgentModels[0];
                }
            }
            catch { /* use defaults */ }

            // Load existing sessions
            try
            {
                var sessions = await _service.ListSessionsAsync();
                foreach (var s in sessions)
                {
                    Sessions.Add(new SessionItemViewModel
                    {
                        SessionId = s.SessionId,
                        AgentName = s.AgentName,
                        Status = s.Status.ToString(),
                    });
                }
            }
            catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Connection failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync()
    {
        if (!IsConnected) return;

        AddEvent("log", $"Creating session with {SelectedAgent}...");
        try
        {
            var session = await _service.CreateSessionAsync(SelectedAgent);
            var item = new SessionItemViewModel
            {
                SessionId = session.SessionId,
                AgentName = session.AgentName,
                Status = session.Status.ToString(),
            };
            Sessions.Add(item);
            AddEvent("log", $"Session created: {session.SessionId[..8]}...");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Failed to create session: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteSessionAsync()
    {
        if (SelectedSession == null) return;
        var id = SelectedSession.SessionId;

        try
        {
            StopStreams();
            await _service.DeleteSessionAsync(id);
            Sessions.Remove(SelectedSession);
            SelectedSession = null;
            SessionStatusText = "Session: --";
            CanSend = false;
            SetControlState("idle");
            HasScreenshot = false;
            ScreenshotData = null;
            AddEvent("log", "Session deleted");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Delete failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SendTaskAsync()
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(TaskText)) return;

        var text = TaskText;
        TaskText = string.Empty;
        AddEvent("user", text);
        SetControlState("running");
        SetRunStatus("running");

        try
        {
            await _service.SendMessageAsync(SelectedSession.SessionId, text);
            AddEvent("log", "Message sent to agent");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Send failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        if (SelectedSession == null) return;
        try
        {
            await _service.PauseAsync(SelectedSession.SessionId);
            SetControlState("paused");
            SetRunStatus("paused");
            AddEvent("log", "Session paused");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Pause failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ResumeAsync()
    {
        if (SelectedSession == null) return;
        try
        {
            await _service.ResumeAsync(SelectedSession.SessionId);
            SetControlState("running");
            SetRunStatus("running");
            AddEvent("log", "Session resumed");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Resume failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedSession == null) return;
        try
        {
            await _service.CancelAsync(SelectedSession.SessionId);
            SetControlState("idle");
            SetRunStatus("ready");
            AddEvent("log", "Session cancelled");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Cancel failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void FillQuickCard(string task)
    {
        TaskText = task;
    }

    // --- Session Selection ---

    partial void OnSelectedSessionChanged(SessionItemViewModel? value)
    {
        if (value == null)
        {
            CanSend = false;
            return;
        }

        StopStreams();
        Events.Clear();
        HasScreenshot = false;
        ScreenshotData = null;
        CanSend = true;
        SessionStatusText = $"Session: {value.SessionId[..Math.Min(8, value.SessionId.Length)]}...";

        AddEvent("log", $"Selected session {value.SessionId[..8]}...");

        StartStream(value.SessionId);
        StartScreenshots(value.SessionId);
    }

    // --- Streaming ---

    private void StartStream(string sessionId)
    {
        _streamCts = new CancellationTokenSource();
        var ct = _streamCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in _service.StreamEventsAsync(sessionId, ct))
                {
                    if (ct.IsCancellationRequested) break;

                    var eventType = evt.Event.ToString().ToLowerInvariant();
                    var content = "";
                    try
                    {
                        var data = evt.Data;
                        if (data.ValueKind == JsonValueKind.Object)
                        {
                            if (data.TryGetProperty("content", out var c))
                                content = c.ToString();
                            else if (data.TryGetProperty("message", out var m))
                                content = m.ToString();
                            else if (data.TryGetProperty("text", out var t))
                                content = t.ToString();
                            else
                                content = data.ToString();
                        }
                        else
                        {
                            content = data.ToString();
                        }
                    }
                    catch
                    {
                        content = evt.Data.ToString();
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AddEvent(eventType, content);

                        if (eventType is "done")
                        {
                            SetControlState("idle");
                            SetRunStatus("finished");
                        }
                        else if (eventType is "error")
                        {
                            SetControlState("idle");
                            SetRunStatus("error");
                        }
                        else if (eventType is "paused")
                        {
                            SetControlState("paused");
                            SetRunStatus("paused");
                        }
                        else if (eventType is "resumed")
                        {
                            SetControlState("running");
                            SetRunStatus("running");
                        }
                    });

                    if (eventType is "done" or "error") break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        AddEvent("error", $"Stream error: {ex.Message}"));
                }
            }
        }, ct);
    }

    // --- Screenshots ---

    private void StartScreenshots(string sessionId)
    {
        _screenshotTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        var fetching = false;

        _screenshotTimer.Tick += async (_, _) =>
        {
            if (fetching || SelectedSession?.SessionId != sessionId) return;
            fetching = true;
            try
            {
                var screenshot = await _service.ScreenshotAsync(sessionId);
                var data = screenshot.Data;
                // Strip data URL prefix if present
                if (data.Contains(','))
                    data = data.Split(',')[1];
                var bytes = System.Convert.FromBase64String(data);
                ScreenshotData = bytes;
                HasScreenshot = true;
            }
            catch { /* silently ignore */ }
            finally { fetching = false; }
        };

        _screenshotTimer.Start();
    }

    private void StopStreams()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;

        _screenshotTimer?.Stop();
        _screenshotTimer = null;
    }

    // --- Helpers ---

    private void AddEvent(string type, string content)
    {
        Events.Add(EventItemViewModel.Create(type, content));
    }

    private void SetControlState(string state)
    {
        var running = state == "running";
        var paused = state == "paused";
        CanPause = running;
        CanResume = paused;
        CanCancel = running || paused;
    }

    private void SetRunStatus(string status)
    {
        RunStatusText = char.ToUpper(status[0]) + status[1..];
        RunStatusColor = status switch
        {
            "running" => "#2ecc71",
            "paused" => "#f39c12",
            "error" => "#e74c3c",
            "finished" => "#3498db",
            "ready" => "#95a5a6",
            _ => "#888888",
        };
    }

    public void Dispose()
    {
        StopStreams();
        _service.Dispose();
    }
}
