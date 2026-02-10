# Multimodal Driver Support - C# SDK Updates

This update adds comprehensive multimodal support to the C# SDK to match the new agi-driver capabilities.

## Changes Required

### Protocol Updates (`src/Agi/Driver/Protocol.cs`)

#### Add to DriverEventType Enum
```csharp
AudioTranscript,
VideoFrame,
SpeechStarted,
SpeechFinishedEvent,
TurnDetected
```

#### Add to DriverCommandType Enum
```csharp
GetAudioTranscript,
GetVideoFrame
```

#### New Classes

```csharp
// MCP server configuration
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

// Agent identity
public class AgentIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "agi-2-claude";

    [JsonPropertyName("creator")]
    public string Creator { get; set; } = "AGI Company";

    [JsonPropertyName("creator_url")]
    public string CreatorUrl { get; set; } = "https://theagi.company";
}

// Tool choice (with custom converter for string or object)
[JsonConverter(typeof(ToolChoiceConverter))]
public class ToolChoice
{
    public string Mode { get; set; } = "auto";
    public string? ToolName { get; set; }
}

// New event classes
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

// New command classes
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
```

#### Add to StartCommand Class

```csharp
// Multimodal features
[JsonPropertyName("agent_identity")]
public AgentIdentity? AgentIdentity { get; set; }

[JsonPropertyName("tool_choice")]
public ToolChoice? ToolChoice { get; set; }

[JsonPropertyName("mcp_servers")]
public MCPServerConfig[]? McpServers { get; set; }

[JsonPropertyName("audio_input_enabled")]
public bool AudioInputEnabled { get; set; } = false;

[JsonPropertyName("audio_buffer_seconds")]
public int AudioBufferSeconds { get; set; } = 30;

[JsonPropertyName("turn_detection_enabled")]
public bool TurnDetectionEnabled { get; set; } = false;

[JsonPropertyName("turn_detection_silence_ms")]
public int TurnDetectionSilenceMs { get; set; } = 1000;

[JsonPropertyName("speech_output_enabled")]
public bool SpeechOutputEnabled { get; set; } = false;

[JsonPropertyName("speech_voice")]
public string SpeechVoice { get; set; } = "alloy";

[JsonPropertyName("camera_enabled")]
public bool CameraEnabled { get; set; } = false;

[JsonPropertyName("camera_buffer_seconds")]
public int CameraBufferSeconds { get; set; } = 30;

[JsonPropertyName("screen_recording_enabled")]
public bool ScreenRecordingEnabled { get; set; } = false;

[JsonPropertyName("screen_recording_buffer_seconds")]
public int ScreenRecordingBufferSeconds { get; set; } = 30;
```

#### Update AgentName Default

Change `AgentName` property default from `""` to `"agi-2-claude"`.

## Usage Examples

### Basic Multimodal Session

```csharp
using Agi.Driver;

var driver = new AgentDriver(new DriverOptions
{
    Mode = "local",
    AgentName = "agi-2-claude"
});

var result = await driver.Start(new StartCommand
{
    Goal = "Help me with my computer",
    Mode = "local",
    AgentName = "agi-2-claude",

    // Voice features
    AudioInputEnabled = true,
    TurnDetectionEnabled = true,
    SpeechOutputEnabled = true,
    SpeechVoice = "alloy",

    // Video features
    CameraEnabled = true,
    ScreenRecordingEnabled = true,

    // MCP servers
    McpServers = new[]
    {
        new MCPServerConfig
        {
            Name = "filesystem",
            Command = "npx",
            Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "/path/to/dir" }
        }
    },

    // Tool choice
    ToolChoice = new ToolChoice { Mode = "auto" }
});
```

### Handling New Events

```csharp
driver.OnEvent += (event) =>
{
    switch (event)
    {
        case AudioTranscriptEvent ate:
            Console.WriteLine($"Transcript: {ate.Transcript}");
            break;

        case VideoFrameEvent vfe:
            SaveFrame(vfe.FrameBase64);
            break;

        case SpeechStartedEvent sse:
            Console.WriteLine($"🔊 Speaking: {sse.Text}");
            break;

        case SpeechFinishedEvent:
            Console.WriteLine("✓ Finished speaking");
            break;

        case TurnDetectedEvent tde:
            Console.WriteLine($"You said: {tde.Transcript}");
            break;
    }
};
```

### Voice-Only Mode

```csharp
var result = await driver.Start(new StartCommand
{
    Goal = "(voice input)",
    Mode = "local",
    AudioInputEnabled = true,
    TurnDetectionEnabled = true,
    TurnDetectionSilenceMs = 1000, // 1 second of silence = turn complete
    SpeechOutputEnabled = true,
    SpeechVoice = "alloy" // or: echo, fable, onyx, nova, shimmer
});
```

### MCP Servers

```csharp
var mcpServers = new[]
{
    new MCPServerConfig
    {
        Name = "filesystem",
        Command = "npx",
        Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "/Users/you/Documents" }
    },
    new MCPServerConfig
    {
        Name = "database",
        Command = "python",
        Args = new[] { "-m", "my_db_server" },
        Env = new Dictionary<string, string> { { "DATABASE_URL", "postgresql://..." } }
    }
};

await driver.Start(new StartCommand
{
    Goal = "Analyze my documents",
    Mode = "local",
    McpServers = mcpServers
});
```

### Tool Choice Configuration

```csharp
// Auto (default)
ToolChoice = new ToolChoice { Mode = "auto" }

// Required - must use at least one tool
ToolChoice = new ToolChoice { Mode = "required" }

// None - no tool use
ToolChoice = new ToolChoice { Mode = "none" }

// Specific tool
ToolChoice = new ToolChoice { Mode = "tool", ToolName = "filesystem__read_file" }
```

## Breaking Changes

⚠️ This is a breaking change with no backwards compatibility.

- `StartCommand` has many new fields (all have defaults)
- New event types may be emitted
- `AgentName` default should be changed to `"agi-2-claude"`

## Testing

```bash
# Build project
dotnet build

# Run tests
dotnet test

# Try a voice session (example)
dotnet run --project examples/VoiceExample
```

## Implementation Checklist

- [ ] Add new event types to `DriverEventType` enum
- [ ] Add new command types to `DriverCommandType` enum
- [ ] Add new event classes (AudioTranscriptEvent, etc.)
- [ ] Add new command classes (GetAudioTranscriptCommand, etc.)
- [ ] Add helper classes (MCPServerConfig, AgentIdentity, ToolChoice)
- [ ] Update StartCommand with multimodal fields
- [ ] Update event parsing in Driver.cs
- [ ] Change AgentName default to "agi-2-claude"
- [ ] Add XML documentation comments
- [ ] Update README with multimodal examples
- [ ] Add integration tests
- [ ] Add voice/video example projects

## Related PRs

- agi-api (driver): https://github.com/agi-inc/agents/pull/344
- agi-python: https://github.com/agi-inc/agi-python/pull/8
- agi-node: https://github.com/agi-inc/agi-node/pull/11

## Note

Due to the C# SDK's strongly-typed nature, these changes require more extensive code updates compared to Python/Node. A Protocol_Multimodal.cs file has been created as a reference implementation. Integrate these changes into the existing Protocol.cs file.
