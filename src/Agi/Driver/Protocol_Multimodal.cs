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
    Error,
    ScreenshotCaptured,
    SessionCreated,
    AudioTranscript,
    VideoFrame,
    SpeechStarted,
    SpeechFinished,
    TurnDetected
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
    Answer,
    GetAudioTranscript,
    GetVideoFrame
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
/// MCP server configuration.
/// </summary>
public class MCPServerConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("args")]
    public string[] Args { get; set; } = Array.Empty<string>();

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }
}

/// <summary>
/// Agent identity information.
/// </summary>
public class AgentIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "agi-2-claude";

    [JsonPropertyName("creator")]
    public string Creator { get; set; } = "AGI Company";

    [JsonPropertyName("creator_url")]
    public string CreatorUrl { get; set; } = "https://theagi.company";
}

/// <summary>
/// Tool choice configuration.
/// </summary>
[JsonConverter(typeof(ToolChoiceConverter))]
public class ToolChoice
{
    public string Mode { get; set; } = "auto"; // "auto", "required", "none"
    public string? ToolName { get; set; } // For specific tool selection
}

public class ToolChoiceConverter : JsonConverter<ToolChoice>
{
    public override ToolChoice Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new ToolChoice { Mode = reader.GetString() ?? "auto" };
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            return new ToolChoice
            {
                Mode = "tool",
                ToolName = root.GetProperty("name").GetString()
            };
        }
        throw new JsonException("Invalid tool_choice format");
    }

    public override void Write(Utf8JsonWriter writer, ToolChoice value, JsonSerializerOptions options)
    {
        if (value.Mode == "tool" && value.ToolName != null)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "tool");
            writer.WriteString("name", value.ToolName);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStringValue(value.Mode);
        }
    }
}

// New multimodal event classes
public class AudioTranscriptEvent : BaseDriverEvent
{
    public override string EventName => "audio_transcript";

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = "";

    [JsonPropertyName("seconds_ago")]
    public int SecondsAgo { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}

public class VideoFrameEvent : BaseDriverEvent
{
    public override string EventName => "video_frame";

    [JsonPropertyName("frame_base64")]
    public string FrameBase64 { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("seconds_ago")]
    public int SecondsAgo { get; set; }
}

public class SpeechStartedEvent : BaseDriverEvent
{
    public override string EventName => "speech_started";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public class SpeechFinishedEvent : BaseDriverEvent
{
    public override string EventName => "speech_finished";
}

public class TurnDetectedEvent : BaseDriverEvent
{
    public override string EventName => "turn_detected";

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = "";
}

// New multimodal command classes
public class GetAudioTranscriptCommand
{
    [JsonPropertyName("command")]
    public string Command => "get_audio_transcript";

    [JsonPropertyName("seconds_ago")]
    public int SecondsAgo { get; set; } = 5;

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = 5;
}

public class GetVideoFrameCommand
{
    [JsonPropertyName("command")]
    public string Command => "get_video_frame";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "screen";

    [JsonPropertyName("seconds_ago")]
    public int SecondsAgo { get; set; } = 1;
}

// Updated StartCommand with multimodal fields (add these fields to existing StartCommand class)
