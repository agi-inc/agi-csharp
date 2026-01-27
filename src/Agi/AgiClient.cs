using Agi.Resources;
using Agi.Context;
using Agi.Types;

namespace Agi;

/// <summary>
/// Options for creating an AGI client
/// </summary>
public class AgiClientOptions
{
    /// <summary>
    /// API key for authentication. If not provided, reads from AGI_API_KEY environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the API. Defaults to https://api.agi.tech
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.agi.tech";

    /// <summary>
    /// Request timeout. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum number of retries for transient failures. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Main client for the AGI API
/// </summary>
public class AgiClient : IDisposable
{
    private readonly AgiHttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Sessions resource for managing browser automation sessions
    /// </summary>
    public SessionsResource Sessions { get; }

    /// <summary>
    /// Create a new AGI client with default options.
    /// API key is read from AGI_API_KEY environment variable.
    /// </summary>
    public AgiClient() : this(new AgiClientOptions())
    {
    }

    /// <summary>
    /// Create a new AGI client with the specified API key
    /// </summary>
    public AgiClient(string apiKey) : this(new AgiClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>
    /// Create a new AGI client with the specified options
    /// </summary>
    public AgiClient(AgiClientOptions options)
    {
        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("AGI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new AuthenticationException(
                "API key is required. Provide it in options or set AGI_API_KEY environment variable.");
        }

        _httpClient = new AgiHttpClient(
            apiKey,
            options.BaseUrl,
            options.Timeout,
            options.MaxRetries);

        Sessions = new SessionsResource(_httpClient);
    }

    /// <summary>
    /// Create a session context with automatic cleanup.
    /// Use with 'await using' for automatic disposal.
    /// </summary>
    /// <param name="agentName">Agent name (default: "agi-0")</param>
    /// <param name="options">Session creation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session context that disposes the session when done</returns>
    /// <example>
    /// <code>
    /// await using var session = await client.SessionAsync("agi-0", new SessionCreateOptions
    /// {
    ///     AgentSessionType = AgentSessionType.Desktop
    /// });
    ///
    /// var result = await session.RunTaskAsync("Find flights from SFO to JFK");
    /// </code>
    /// </example>
    public async Task<SessionContext> SessionAsync(
        string agentName = "agi-0",
        SessionCreateOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var session = await Sessions.CreateAsync(agentName, options, cancellationToken);
        return new SessionContext(this, session);
    }

    /// <summary>
    /// Create a desktop session context for client-driven automation.
    /// Use with 'await using' for automatic disposal.
    /// </summary>
    /// <param name="agentName">Agent name (default: "agi-0")</param>
    /// <param name="options">Additional session options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session context configured for desktop mode</returns>
    /// <example>
    /// <code>
    /// await using var session = await client.DesktopSessionAsync();
    ///
    /// var loop = session.CreateAgentLoop(
    ///     client,
    ///     captureScreenshot: async () => await CaptureScreen(),
    ///     executeActions: async (actions) => await ExecuteDesktopActions(actions)
    /// );
    ///
    /// var result = await loop.StartAsync("Open calculator and compute 2+2");
    /// </code>
    /// </example>
    public async Task<SessionContext> DesktopSessionAsync(
        string agentName = "agi-0",
        SessionCreateOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SessionCreateOptions();
        options.AgentSessionType = AgentSessionType.Desktop;

        var session = await Sessions.CreateAsync(agentName, options, cancellationToken);
        return new SessionContext(this, session);
    }

    /// <summary>
    /// Dispose the client and release resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
