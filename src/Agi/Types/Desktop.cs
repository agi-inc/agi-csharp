using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agi.Types;

/// <summary>
/// A desktop action to be executed by the client
/// </summary>
public class DesktopAction
{
    /// <summary>
    /// Action type
    /// </summary>
    [JsonPropertyName("type")]
    public DesktopActionType Type { get; set; }

    /// <summary>
    /// X coordinate for click/drag actions
    /// </summary>
    [JsonPropertyName("x")]
    public int? X { get; set; }

    /// <summary>
    /// Y coordinate for click/drag actions
    /// </summary>
    [JsonPropertyName("y")]
    public int? Y { get; set; }

    /// <summary>
    /// Click type for click actions
    /// </summary>
    [JsonPropertyName("click_type")]
    public ClickType? ClickType { get; set; }

    /// <summary>
    /// Text for type actions
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Scroll direction
    /// </summary>
    [JsonPropertyName("direction")]
    public ScrollDirection? Direction { get; set; }

    /// <summary>
    /// Scroll amount
    /// </summary>
    [JsonPropertyName("amount")]
    public int? Amount { get; set; }

    /// <summary>
    /// Hotkey combination (e.g., "Ctrl+a")
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Start X coordinate for drag actions
    /// </summary>
    [JsonPropertyName("start_x")]
    public int? StartX { get; set; }

    /// <summary>
    /// Start Y coordinate for drag actions
    /// </summary>
    [JsonPropertyName("start_y")]
    public int? StartY { get; set; }

    /// <summary>
    /// End X coordinate for drag actions
    /// </summary>
    [JsonPropertyName("end_x")]
    public int? EndX { get; set; }

    /// <summary>
    /// End Y coordinate for drag actions
    /// </summary>
    [JsonPropertyName("end_y")]
    public int? EndY { get; set; }

    /// <summary>
    /// Content for type actions (alternative to text)
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Duration in seconds for wait actions
    /// </summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>
    /// Additional properties not explicitly mapped
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Response from the step_desktop endpoint
/// </summary>
public class StepDesktopResponse
{
    /// <summary>
    /// List of actions to execute
    /// </summary>
    [JsonPropertyName("actions")]
    public List<DesktopAction> Actions { get; set; } = new();

    /// <summary>
    /// Agent's reasoning/thinking
    /// </summary>
    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }

    /// <summary>
    /// Whether the task is complete
    /// </summary>
    [JsonPropertyName("finished")]
    public bool Finished { get; set; }

    /// <summary>
    /// Question for the user if input is needed
    /// </summary>
    [JsonPropertyName("askUser")]
    public string? AskUser { get; set; }

    /// <summary>
    /// Current step number
    /// </summary>
    [JsonPropertyName("step")]
    public int Step { get; set; }
}

/// <summary>
/// Request body for step_desktop endpoint
/// </summary>
public class StepDesktopRequest
{
    /// <summary>
    /// Base64-encoded screenshot
    /// </summary>
    [JsonPropertyName("screenshot")]
    public string Screenshot { get; set; } = string.Empty;

    /// <summary>
    /// Session ID for routing
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional message (typically for first step)
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// User response to a question
    /// </summary>
    [JsonPropertyName("user_response")]
    public string? UserResponse { get; set; }
}

/// <summary>
/// Model information from list models endpoint
/// </summary>
public class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }
}

/// <summary>
/// Response from list models endpoint
/// </summary>
public class ListModelsResponse
{
    [JsonPropertyName("models")]
    public List<ModelInfo> Models { get; set; } = new();
}
