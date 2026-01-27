using System.Text.Json.Serialization;

namespace Agi.Types;

/// <summary>
/// Session status values
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<SessionStatus>))]
public enum SessionStatus
{
    [JsonPropertyName("ready")]
    Ready,

    [JsonPropertyName("running")]
    Running,

    [JsonPropertyName("waiting_for_input")]
    WaitingForInput,

    [JsonPropertyName("paused")]
    Paused,

    [JsonPropertyName("finished")]
    Finished,

    [JsonPropertyName("error")]
    Error
}

/// <summary>
/// Message types in session conversations
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<MessageType>))]
public enum MessageType
{
    [JsonPropertyName("THOUGHT")]
    Thought,

    [JsonPropertyName("QUESTION")]
    Question,

    [JsonPropertyName("USER")]
    User,

    [JsonPropertyName("DONE")]
    Done,

    [JsonPropertyName("ERROR")]
    Error,

    [JsonPropertyName("LOG")]
    Log
}

/// <summary>
/// Server-sent event types
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<EventType>))]
public enum EventType
{
    [JsonPropertyName("step")]
    Step,

    [JsonPropertyName("thought")]
    Thought,

    [JsonPropertyName("question")]
    Question,

    [JsonPropertyName("done")]
    Done,

    [JsonPropertyName("error")]
    Error,

    [JsonPropertyName("log")]
    Log,

    [JsonPropertyName("paused")]
    Paused,

    [JsonPropertyName("resumed")]
    Resumed,

    [JsonPropertyName("heartbeat")]
    Heartbeat,

    [JsonPropertyName("user")]
    User
}

/// <summary>
/// Desktop action types for client-driven sessions
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<DesktopActionType>))]
public enum DesktopActionType
{
    [JsonPropertyName("click")]
    Click,

    [JsonPropertyName("type")]
    Type,

    [JsonPropertyName("scroll")]
    Scroll,

    [JsonPropertyName("hotkey")]
    Hotkey,

    [JsonPropertyName("drag")]
    Drag,

    [JsonPropertyName("wait")]
    Wait,

    [JsonPropertyName("finished")]
    Finished,

    [JsonPropertyName("await_user_input")]
    AwaitUserInput
}

/// <summary>
/// Click types for click actions
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<ClickType>))]
public enum ClickType
{
    [JsonPropertyName("left")]
    Left,

    [JsonPropertyName("right")]
    Right,

    [JsonPropertyName("double")]
    Double,

    [JsonPropertyName("middle")]
    Middle
}

/// <summary>
/// Scroll directions
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<ScrollDirection>))]
public enum ScrollDirection
{
    [JsonPropertyName("up")]
    Up,

    [JsonPropertyName("down")]
    Down,

    [JsonPropertyName("left")]
    Left,

    [JsonPropertyName("right")]
    Right
}

/// <summary>
/// Session types for agent sessions
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<AgentSessionType>))]
public enum AgentSessionType
{
    /// <summary>
    /// API manages browser (server-driven)
    /// </summary>
    [JsonPropertyName("managed-cdp")]
    ManagedCdp,

    /// <summary>
    /// External browser via CDP WebSocket (server-driven)
    /// </summary>
    [JsonPropertyName("external-cdp")]
    ExternalCdp,

    /// <summary>
    /// Client-managed execution loop (desktop mode)
    /// </summary>
    [JsonPropertyName("desktop")]
    Desktop
}

/// <summary>
/// Save snapshot modes when deleting sessions
/// </summary>
[JsonConverter(typeof(JsonEnumMemberConverter<SaveSnapshotMode>))]
public enum SaveSnapshotMode
{
    [JsonPropertyName("none")]
    None,

    [JsonPropertyName("auto")]
    Auto,

    [JsonPropertyName("always")]
    Always
}
