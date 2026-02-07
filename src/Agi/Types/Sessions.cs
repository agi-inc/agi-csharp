using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agi.Types;

/// <summary>
/// Options for creating a new session
/// </summary>
public class SessionCreateOptions
{
    /// <summary>
    /// Maximum number of steps the agent can take
    /// </summary>
    [JsonPropertyName("max_steps")]
    public int? MaxSteps { get; set; }

    /// <summary>
    /// Webhook URL to receive status updates
    /// </summary>
    [JsonPropertyName("webhook_url")]
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Initial goal or task description
    /// </summary>
    [JsonPropertyName("goal")]
    public string? Goal { get; set; }

    /// <summary>
    /// Session type: managed-cdp, external-cdp, or desktop
    /// </summary>
    [JsonPropertyName("agent_session_type")]
    public AgentSessionType? AgentSessionType { get; set; }

    /// <summary>
    /// Environment ID to restore from a snapshot
    /// </summary>
    [JsonPropertyName("restore_from_environment_id")]
    public string? RestoreFromEnvironmentId { get; set; }

    /// <summary>
    /// CDP WebSocket URL for external browser connections
    /// </summary>
    [JsonPropertyName("cdp_ws_url")]
    public string? CdpWsUrl { get; set; }

    /// <summary>
    /// Model to use for the agent
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Cloud environment type: "chrome-1" (Chrome + desktop VM) or "ubuntu-1" (desktop-only VM)
    /// </summary>
    [JsonPropertyName("environment_type")]
    public string? EnvironmentType { get; set; }
}

/// <summary>
/// Response from session creation or retrieval
/// </summary>
public class SessionResponse
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// VNC URL for browser access (server-driven sessions)
    /// </summary>
    [JsonPropertyName("vnc_url")]
    public string? VncUrl { get; set; }

    /// <summary>
    /// Agent URL for desktop mode step requests
    /// </summary>
    [JsonPropertyName("agent_url")]
    public string? AgentUrl { get; set; }

    /// <summary>
    /// Name of the agent
    /// </summary>
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Current session status
    /// </summary>
    [JsonPropertyName("status")]
    public SessionStatus Status { get; set; }

    /// <summary>
    /// Session creation timestamp
    /// </summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Environment ID for snapshots
    /// </summary>
    [JsonPropertyName("environment_id")]
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// Current goal or task
    /// </summary>
    [JsonPropertyName("goal")]
    public string? Goal { get; set; }

    /// <summary>
    /// Session type
    /// </summary>
    [JsonPropertyName("agent_session_type")]
    public string? AgentSessionType { get; set; }
}

/// <summary>
/// Response from status endpoint
/// </summary>
public class ExecuteStatusResponse
{
    /// <summary>
    /// Current session status
    /// </summary>
    [JsonPropertyName("status")]
    public SessionStatus Status { get; set; }
}

/// <summary>
/// A message in the session conversation
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// Message ID
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Message type
    /// </summary>
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    /// <summary>
    /// Message content (can be string or object)
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    /// <summary>
    /// Message timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }

    /// <summary>
    /// Get content as string
    /// </summary>
    public string? GetContentAsString()
    {
        if (Content.ValueKind == JsonValueKind.String)
            return Content.GetString();
        return Content.ToString();
    }
}

/// <summary>
/// Response containing multiple messages
/// </summary>
public class MessagesResponse
{
    /// <summary>
    /// List of messages
    /// </summary>
    [JsonPropertyName("messages")]
    public List<MessageResponse> Messages { get; set; } = new();

    /// <summary>
    /// Current status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether the session has an active agent
    /// </summary>
    [JsonPropertyName("hasAgent")]
    public bool HasAgent { get; set; }
}

/// <summary>
/// Server-sent event from streaming endpoint
/// </summary>
public class SSEEvent
{
    /// <summary>
    /// Event ID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Event type
    /// </summary>
    public EventType Event { get; set; }

    /// <summary>
    /// Event data
    /// </summary>
    public JsonElement Data { get; set; }
}

/// <summary>
/// Options for sending a message
/// </summary>
public class SendMessageOptions
{
    /// <summary>
    /// Starting URL for the agent
    /// </summary>
    [JsonPropertyName("start_url")]
    public string? StartUrl { get; set; }

    /// <summary>
    /// Additional context or instructions
    /// </summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

/// <summary>
/// Options for streaming events
/// </summary>
public class StreamEventsOptions
{
    /// <summary>
    /// Event ID to start streaming from
    /// </summary>
    public string? AfterId { get; set; }

    /// <summary>
    /// Timeout for the stream
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// Options for running a task
/// </summary>
public class RunTaskOptions
{
    /// <summary>
    /// Starting URL
    /// </summary>
    public string? StartUrl { get; set; }

    /// <summary>
    /// Additional context
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Polling interval
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Task timeout
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Callback for status updates
    /// </summary>
    public Action<SessionStatus>? OnStatusChange { get; set; }

    /// <summary>
    /// Callback for new messages
    /// </summary>
    public Action<MessageResponse>? OnMessage { get; set; }
}

/// <summary>
/// Response from sending a message
/// </summary>
public class SendMessageResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response from listing sessions
/// </summary>
public class ListSessionsResponse
{
    [JsonPropertyName("sessions")]
    public List<SessionResponse> Sessions { get; set; } = new();
}

/// <summary>
/// Response from delete operations
/// </summary>
public class DeleteResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
