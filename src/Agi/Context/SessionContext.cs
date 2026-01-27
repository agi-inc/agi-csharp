using Agi.Types;
using Agi.Resources;

namespace Agi.Context;

/// <summary>
/// High-level context for managing a session with automatic cleanup
/// </summary>
public class SessionContext : IAsyncDisposable
{
    private readonly AgiClient _client;
    private readonly SessionResponse _session;
    private bool _disposed;

    /// <summary>
    /// Session ID
    /// </summary>
    public string SessionId => _session.SessionId;

    /// <summary>
    /// VNC URL for browser access (server-driven sessions)
    /// </summary>
    public string? VncUrl => _session.VncUrl;

    /// <summary>
    /// Agent URL for desktop mode
    /// </summary>
    public string? AgentUrl => _session.AgentUrl;

    /// <summary>
    /// Agent name
    /// </summary>
    public string AgentName => _session.AgentName;

    /// <summary>
    /// Current status
    /// </summary>
    public SessionStatus Status => _session.Status;

    /// <summary>
    /// Environment ID for snapshots
    /// </summary>
    public string? EnvironmentId => _session.EnvironmentId;

    /// <summary>
    /// Session response
    /// </summary>
    public SessionResponse Session => _session;

    internal SessionContext(AgiClient client, SessionResponse session)
    {
        _client = client;
        _session = session;
    }

    /// <summary>
    /// Run a task and wait for completion
    /// </summary>
    public async Task<TaskResult> RunTaskAsync(
        string task,
        RunTaskOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new RunTaskOptions();
        var startTime = DateTime.UtcNow;
        var messages = new List<MessageResponse>();
        var lastMessageId = 0;
        var lastStatus = SessionStatus.Ready;

        // Send the initial message
        await SendMessageAsync(task, new SendMessageOptions
        {
            StartUrl = opts.StartUrl,
            Context = opts.Context
        }, cancellationToken);

        using var timeoutCts = new CancellationTokenSource(opts.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            while (true)
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                // Get status
                var status = await GetStatusAsync(linkedCts.Token);

                if (status.Status != lastStatus)
                {
                    lastStatus = status.Status;
                    opts.OnStatusChange?.Invoke(status.Status);
                }

                // Get new messages
                var messagesResponse = await GetMessagesAsync(lastMessageId, cancellationToken: linkedCts.Token);

                foreach (var msg in messagesResponse.Messages)
                {
                    if (msg.Id > lastMessageId)
                    {
                        lastMessageId = msg.Id;
                        messages.Add(msg);
                        opts.OnMessage?.Invoke(msg);
                    }
                }

                // Check if done
                if (status.Status == SessionStatus.Finished ||
                    status.Status == SessionStatus.Error)
                {
                    var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                    var success = status.Status == SessionStatus.Finished;

                    // Find the DONE message for result data
                    var doneMessage = messages.LastOrDefault(m => m.Type == MessageType.Done);

                    return new TaskResult
                    {
                        Data = doneMessage?.Content,
                        Metadata = new TaskMetadata
                        {
                            TaskId = 0, // Not provided by API
                            SessionId = SessionId,
                            Duration = duration,
                            Cost = 0, // Not provided by API
                            Timestamp = DateTime.UtcNow,
                            Steps = messages.Count(m => m.Type == MessageType.Thought),
                            Success = success,
                            Messages = messages
                        }
                    };
                }

                // Check if waiting for input
                if (status.Status == SessionStatus.WaitingForInput)
                {
                    var question = messages.LastOrDefault(m => m.Type == MessageType.Question);
                    throw new AgentExecutionException(
                        $"Agent is waiting for input: {question?.GetContentAsString() ?? "Unknown question"}",
                        SessionId);
                }

                await Task.Delay(opts.PollInterval, linkedCts.Token);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new Agi.TimeoutException("Task timed out", opts.Timeout);
        }
    }

    #region Wrapped SessionsResource Methods

    /// <summary>
    /// Send a message to the session
    /// </summary>
    public Task<SendMessageResponse> SendMessageAsync(
        string message,
        SendMessageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _client.Sessions.SendMessageAsync(SessionId, message, options, cancellationToken);
    }

    /// <summary>
    /// Get the current status
    /// </summary>
    public Task<ExecuteStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return _client.Sessions.GetStatusAsync(SessionId, cancellationToken);
    }

    /// <summary>
    /// Get messages
    /// </summary>
    public Task<MessagesResponse> GetMessagesAsync(
        int? afterId = null,
        bool? sanitize = null,
        CancellationToken cancellationToken = default)
    {
        return _client.Sessions.GetMessagesAsync(SessionId, afterId, sanitize, cancellationToken);
    }

    /// <summary>
    /// Stream events
    /// </summary>
    public IAsyncEnumerable<SSEEvent> StreamEventsAsync(
        StreamEventsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _client.Sessions.StreamEventsAsync(SessionId, options, cancellationToken);
    }

    /// <summary>
    /// Pause the session
    /// </summary>
    public Task<ExecuteStatusResponse> PauseAsync(CancellationToken cancellationToken = default)
    {
        return _client.Sessions.PauseAsync(SessionId, cancellationToken);
    }

    /// <summary>
    /// Resume the session
    /// </summary>
    public Task<ExecuteStatusResponse> ResumeAsync(CancellationToken cancellationToken = default)
    {
        return _client.Sessions.ResumeAsync(SessionId, cancellationToken);
    }

    /// <summary>
    /// Cancel the session
    /// </summary>
    public Task<ExecuteStatusResponse> CancelAsync(CancellationToken cancellationToken = default)
    {
        return _client.Sessions.CancelAsync(SessionId, cancellationToken);
    }

    /// <summary>
    /// Navigate to a URL
    /// </summary>
    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        return _client.Sessions.NavigateAsync(SessionId, url, cancellationToken);
    }

    /// <summary>
    /// Take a screenshot
    /// </summary>
    public Task<Screenshot> ScreenshotAsync(CancellationToken cancellationToken = default)
    {
        return _client.Sessions.ScreenshotAsync(SessionId, cancellationToken);
    }

    #endregion

    /// <summary>
    /// Dispose and delete the session
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            await _client.Sessions.DeleteAsync(SessionId);
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }
}
