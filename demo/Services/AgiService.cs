using Agi;
using Agi.Types;

namespace AgiDemo.Services;

/// <summary>
/// Wraps AgiClient for ViewModel consumption.
/// </summary>
public class AgiService : IDisposable
{
    private AgiClient? _client;

    public bool IsConnected => _client != null;

    public void Connect(string apiKey)
    {
        _client?.Dispose();
        _client = new AgiClient(apiKey);
    }

    public async Task<SessionResponse> CreateSessionAsync(string agentName, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _client!.Sessions.CreateAsync(agentName, cancellationToken: ct);
    }

    public async Task<List<SessionResponse>> ListSessionsAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        return await _client!.Sessions.ListAsync(ct);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.Sessions.DeleteAsync(sessionId, cancellationToken: ct);
    }

    public async Task SendMessageAsync(string sessionId, string message, CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.Sessions.SendMessageAsync(sessionId, message, cancellationToken: ct);
    }

    public async Task PauseAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.Sessions.PauseAsync(sessionId, ct);
    }

    public async Task ResumeAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.Sessions.ResumeAsync(sessionId, ct);
    }

    public async Task CancelAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.Sessions.CancelAsync(sessionId, ct);
    }

    public async Task<Screenshot> ScreenshotAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _client!.Sessions.ScreenshotAsync(sessionId, ct);
    }

    public IAsyncEnumerable<SSEEvent> StreamEventsAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        return _client!.Sessions.StreamEventsAsync(sessionId, cancellationToken: ct);
    }

    public async Task<List<ModelInfo>> ListModelsAsync(string? filter = null, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _client!.Sessions.ListModelsAsync(filter, ct);
    }

    private void EnsureConnected()
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected. Call Connect() first.");
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
