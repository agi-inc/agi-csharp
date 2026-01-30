using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agi.Driver;

/// <summary>
/// Event types emitted by the driver.
/// </summary>
public enum DriverEventType
{
    Ready,
    StateChange,
    Thinking,
    Action,
    Confirm,
    AskQuestion,
    Finished,
    Error
}

/// <summary>
/// Command types sent to the driver.
/// </summary>
public enum DriverCommandType
{
    Start,
    Screenshot,
    Pause,
    Resume,
    Stop,
    Confirm,
    Answer
}

/// <summary>
/// Driver execution states.
/// </summary>
public enum DriverState
{
    Idle,
    Running,
    Paused,
    WaitingConfirmation,
    WaitingAnswer,
    Finished,
    Stopped,
    Error
}

/// <summary>
/// An action from the driver.
/// </summary>
public class DriverAction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <summary>
    /// Get a parameter value by key.
    /// </summary>
    public T? GetParameter<T>(string key)
    {
        if (AdditionalProperties == null || !AdditionalProperties.TryGetValue(key, out var element))
            return default;

        return element.Deserialize<T>();
    }
}

/// <summary>
/// Base class for driver events.
/// </summary>
public abstract class BaseDriverEvent
{
    [JsonPropertyName("event")]
    public abstract string EventName { get; }

    [JsonPropertyName("step")]
    public int Step { get; set; }
}

/// <summary>
/// Driver is ready.
/// </summary>
public class ReadyEvent : BaseDriverEvent
{
    public override string EventName => "ready";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";
}

/// <summary>
/// State has changed.
/// </summary>
public class StateChangeEvent : BaseDriverEvent
{
    public override string EventName => "state_change";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    public DriverState GetState() => State switch
    {
        "idle" => DriverState.Idle,
        "running" => DriverState.Running,
        "paused" => DriverState.Paused,
        "waiting_confirmation" => DriverState.WaitingConfirmation,
        "waiting_answer" => DriverState.WaitingAnswer,
        "finished" => DriverState.Finished,
        "stopped" => DriverState.Stopped,
        "error" => DriverState.Error,
        _ => DriverState.Error
    };
}

/// <summary>
/// Agent is thinking.
/// </summary>
public class ThinkingEvent : BaseDriverEvent
{
    public override string EventName => "thinking";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

/// <summary>
/// Agent wants to execute an action.
/// </summary>
public class ActionEvent : BaseDriverEvent
{
    public override string EventName => "action";

    [JsonPropertyName("action")]
    public DriverAction Action { get; set; } = new();
}

/// <summary>
/// Agent needs confirmation.
/// </summary>
public class ConfirmEvent : BaseDriverEvent
{
    public override string EventName => "confirm";

    [JsonPropertyName("action")]
    public DriverAction Action { get; set; } = new();

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

/// <summary>
/// Agent is asking a question.
/// </summary>
public class AskQuestionEvent : BaseDriverEvent
{
    public override string EventName => "ask_question";

    [JsonPropertyName("question")]
    public string Question { get; set; } = "";

    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = "";
}

/// <summary>
/// Task is complete.
/// </summary>
public class FinishedEvent : BaseDriverEvent
{
    public override string EventName => "finished";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

/// <summary>
/// An error occurred.
/// </summary>
public class ErrorEvent : BaseDriverEvent
{
    public override string EventName => "error";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("recoverable")]
    public bool Recoverable { get; set; }
}

/// <summary>
/// Start command.
/// </summary>
public class StartCommand
{
    [JsonPropertyName("command")]
    public string Command => "start";

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("goal")]
    public string Goal { get; set; } = "";

    [JsonPropertyName("screenshot")]
    public string Screenshot { get; set; } = "";

    [JsonPropertyName("screen_width")]
    public int ScreenWidth { get; set; }

    [JsonPropertyName("screen_height")]
    public int ScreenHeight { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "desktop";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet";
}

/// <summary>
/// Screenshot command.
/// </summary>
public class ScreenshotCommand
{
    [JsonPropertyName("command")]
    public string Command => "screenshot";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("screen_width")]
    public int ScreenWidth { get; set; }

    [JsonPropertyName("screen_height")]
    public int ScreenHeight { get; set; }
}

/// <summary>
/// Pause command.
/// </summary>
public class PauseCommand
{
    [JsonPropertyName("command")]
    public string Command => "pause";
}

/// <summary>
/// Resume command.
/// </summary>
public class ResumeCommand
{
    [JsonPropertyName("command")]
    public string Command => "resume";
}

/// <summary>
/// Stop command.
/// </summary>
public class StopCommand
{
    [JsonPropertyName("command")]
    public string Command => "stop";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Confirm response command.
/// </summary>
public class ConfirmResponseCommand
{
    [JsonPropertyName("command")]
    public string Command => "confirm";

    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Answer command.
/// </summary>
public class AnswerCommand
{
    [JsonPropertyName("command")]
    public string Command => "answer";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("question_id")]
    public string? QuestionId { get; set; }
}

/// <summary>
/// Helper class for parsing driver events.
/// </summary>
public static class DriverProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Parse a JSON line into a driver event.
    /// </summary>
    public static BaseDriverEvent ParseEvent(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var eventType = doc.RootElement.GetProperty("event").GetString();

        return eventType switch
        {
            "ready" => JsonSerializer.Deserialize<ReadyEvent>(line, JsonOptions)!,
            "state_change" => JsonSerializer.Deserialize<StateChangeEvent>(line, JsonOptions)!,
            "thinking" => JsonSerializer.Deserialize<ThinkingEvent>(line, JsonOptions)!,
            "action" => JsonSerializer.Deserialize<ActionEvent>(line, JsonOptions)!,
            "confirm" => JsonSerializer.Deserialize<ConfirmEvent>(line, JsonOptions)!,
            "ask_question" => JsonSerializer.Deserialize<AskQuestionEvent>(line, JsonOptions)!,
            "finished" => JsonSerializer.Deserialize<FinishedEvent>(line, JsonOptions)!,
            "error" => JsonSerializer.Deserialize<ErrorEvent>(line, JsonOptions)!,
            _ => throw new ArgumentException($"Unknown event type: {eventType}")
        };
    }

    /// <summary>
    /// Serialize a command to a JSON line.
    /// </summary>
    public static string SerializeCommand<T>(T command)
    {
        return JsonSerializer.Serialize(command, JsonOptions);
    }
}
