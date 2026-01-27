using Agi.Types;

namespace Agi.Resources;

/// <summary>
/// Resource for managing AGI sessions
/// </summary>
public class SessionsResource
{
    private readonly AgiHttpClient _httpClient;

    internal SessionsResource(AgiHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    #region Session Management

    /// <summary>
    /// Create a new session
    /// </summary>
    /// <param name="agentName">Name of the agent (e.g., "agi-0")</param>
    /// <param name="options">Session creation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<SessionResponse> CreateAsync(
        string agentName = "agi-0",
        SessionCreateOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["agent_name"] = agentName
        };

        if (options != null)
        {
            if (options.MaxSteps.HasValue)
                body["max_steps"] = options.MaxSteps.Value;
            if (options.WebhookUrl != null)
                body["webhook_url"] = options.WebhookUrl;
            if (options.Goal != null)
                body["goal"] = options.Goal;
            if (options.AgentSessionType.HasValue)
                body["agent_session_type"] = options.AgentSessionType.Value.ToString().ToLowerInvariant();
            if (options.RestoreFromEnvironmentId != null)
                body["restore_from_environment_id"] = options.RestoreFromEnvironmentId;
            if (options.CdpWsUrl != null)
                body["cdp_ws_url"] = options.CdpWsUrl;
            if (options.Model != null)
                body["model"] = options.Model;
        }

        return await _httpClient.RequestAsync<SessionResponse>(
            HttpMethod.Post,
            "/v1/sessions",
            body,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// List all sessions
    /// </summary>
    public async Task<List<SessionResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.RequestAsync<ListSessionsResponse>(
            HttpMethod.Get,
            "/v1/sessions",
            cancellationToken: cancellationToken);
        return response.Sessions;
    }

    /// <summary>
    /// Get a session by ID
    /// </summary>
    public async Task<SessionResponse> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<SessionResponse>(
            HttpMethod.Get,
            $"/v1/sessions/{sessionId}",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    public async Task<DeleteResponse> DeleteAsync(
        string sessionId,
        SaveSnapshotMode? saveSnapshotMode = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (saveSnapshotMode.HasValue)
        {
            query["save_snapshot_mode"] = saveSnapshotMode.Value.ToString().ToLowerInvariant();
        }

        return await _httpClient.RequestAsync<DeleteResponse>(
            HttpMethod.Delete,
            $"/v1/sessions/{sessionId}",
            query: query.Count > 0 ? query : null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Delete all sessions
    /// </summary>
    public async Task<DeleteResponse> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<DeleteResponse>(
            HttpMethod.Delete,
            "/v1/sessions",
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Agent Interaction

    /// <summary>
    /// Send a message to a session
    /// </summary>
    public async Task<SendMessageResponse> SendMessageAsync(
        string sessionId,
        string message,
        SendMessageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["message"] = message
        };

        if (options != null)
        {
            if (options.StartUrl != null)
                body["start_url"] = options.StartUrl;
            if (options.Context != null)
                body["context"] = options.Context;
        }

        return await _httpClient.RequestAsync<SendMessageResponse>(
            HttpMethod.Post,
            $"/v1/sessions/{sessionId}/message",
            body,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get the current status of a session
    /// </summary>
    public async Task<ExecuteStatusResponse> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<ExecuteStatusResponse>(
            HttpMethod.Get,
            $"/v1/sessions/{sessionId}/status",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get messages from a session
    /// </summary>
    public async Task<MessagesResponse> GetMessagesAsync(
        string sessionId,
        int? afterId = null,
        bool? sanitize = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (afterId.HasValue)
            query["after_id"] = afterId.Value.ToString();
        if (sanitize.HasValue)
            query["sanitize"] = sanitize.Value.ToString().ToLowerInvariant();

        return await _httpClient.RequestAsync<MessagesResponse>(
            HttpMethod.Get,
            $"/v1/sessions/{sessionId}/messages",
            query: query.Count > 0 ? query : null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stream events from a session using Server-Sent Events
    /// </summary>
    public IAsyncEnumerable<SSEEvent> StreamEventsAsync(
        string sessionId,
        StreamEventsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (options?.AfterId != null)
            query["after_id"] = options.AfterId;

        return _httpClient.StreamEventsAsync(
            $"/v1/sessions/{sessionId}/events",
            query.Count > 0 ? query : null,
            cancellationToken);
    }

    #endregion

    #region Session Control

    /// <summary>
    /// Pause a session
    /// </summary>
    public async Task<ExecuteStatusResponse> PauseAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<ExecuteStatusResponse>(
            HttpMethod.Post,
            $"/v1/sessions/{sessionId}/pause",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Resume a paused session
    /// </summary>
    public async Task<ExecuteStatusResponse> ResumeAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<ExecuteStatusResponse>(
            HttpMethod.Post,
            $"/v1/sessions/{sessionId}/resume",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Cancel a running session
    /// </summary>
    public async Task<ExecuteStatusResponse> CancelAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<ExecuteStatusResponse>(
            HttpMethod.Post,
            $"/v1/sessions/{sessionId}/cancel",
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Browser Control

    /// <summary>
    /// Navigate the browser to a URL
    /// </summary>
    public async Task NavigateAsync(
        string sessionId,
        string url,
        CancellationToken cancellationToken = default)
    {
        await _httpClient.RequestAsync<object>(
            HttpMethod.Post,
            $"/v1/sessions/{sessionId}/navigate",
            new { url },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Take a screenshot of the browser
    /// </summary>
    public async Task<Screenshot> ScreenshotAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.RequestAsync<Screenshot>(
            HttpMethod.Get,
            $"/v1/sessions/{sessionId}/screenshot",
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Desktop Mode

    /// <summary>
    /// Execute a step in desktop mode
    /// </summary>
    /// <param name="agentUrl">Agent URL from session response</param>
    /// <param name="sessionId">Session ID for routing</param>
    /// <param name="screenshot">Base64-encoded screenshot</param>
    /// <param name="message">Optional message (typically for first step)</param>
    /// <param name="userResponse">User response to a question</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<StepDesktopResponse> StepAsync(
        string agentUrl,
        string sessionId,
        string screenshot,
        string? message = null,
        string? userResponse = null,
        CancellationToken cancellationToken = default)
    {
        var request = new StepDesktopRequest
        {
            Screenshot = screenshot,
            SessionId = sessionId,
            Message = message,
            UserResponse = userResponse
        };

        return await _httpClient.RequestUrlAsync<StepDesktopResponse>(
            HttpMethod.Post,
            $"{agentUrl.TrimEnd('/')}/step_desktop",
            request,
            cancellationToken);
    }

    /// <summary>
    /// List available models
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsAsync(
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (filter != null)
            query["filter"] = filter;

        var response = await _httpClient.RequestAsync<ListModelsResponse>(
            HttpMethod.Get,
            "/v1/models",
            query: query.Count > 0 ? query : null,
            cancellationToken: cancellationToken);

        return response.Models;
    }

    #endregion
}
