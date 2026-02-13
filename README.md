# AGI C# SDK

Official C# SDK for AGI - AI-powered browser automation and desktop agents.

## Installation

```bash
dotnet add package Agi
```

## Quick Start

### Server-Driven Mode (Browser Automation)

```csharp
using Agi;

// Create client (reads AGI_API_KEY from environment)
using var client = new AgiClient();

// Create a session with automatic cleanup
await using var session = await client.SessionAsync();

// Run a task and wait for completion
var result = await session.RunTaskAsync(
    "Find the cheapest flight from SFO to JFK next Monday",
    new RunTaskOptions
    {
        StartUrl = "https://google.com/flights",
        OnStatusChange = status => Console.WriteLine($"Status: {status}"),
        OnMessage = msg => Console.WriteLine($"Agent: {msg.GetContentAsString()}")
    });

Console.WriteLine($"Task completed in {result.Metadata.Duration}s");
Console.WriteLine($"Steps taken: {result.Metadata.Steps}");
```

### Desktop Mode (Client-Driven Automation)

> **Note:** Desktop mode is currently feature-gated. For enterprise access, contact [`partner@theagi.company`](mailto:partner@theagi.company).

Desktop mode lets you run the agent on your local machine, controlling your desktop or any application.

```csharp
using Agi;
using Agi.Types;

using var client = new AgiClient();

// Create a desktop session
await using var session = await client.DesktopSessionAsync();

// Create the agent loop
var loop = session.CreateAgentLoop(
    client,
    captureScreenshot: async () =>
    {
        // Capture your screen and return base64-encoded image
        var screenshot = await CaptureScreenAsync();
        return Convert.ToBase64String(screenshot);
    },
    executeActions: async (actions) =>
    {
        foreach (var action in actions)
        {
            switch (action.Type)
            {
                case DesktopActionType.Click:
                    await ClickAtAsync(action.X!.Value, action.Y!.Value);
                    break;
                case DesktopActionType.Type:
                    await TypeTextAsync(action.Text!);
                    break;
                case DesktopActionType.Scroll:
                    await ScrollAsync(action.Direction!.Value, action.Amount ?? 3);
                    break;
                case DesktopActionType.Hotkey:
                    await SendHotkeyAsync(action.Key!);
                    break;
                // ... handle other action types
            }
        }
    },
    configure: opts =>
    {
        opts.OnThinking = thinking => Console.WriteLine($"Thinking: {thinking}");
        opts.OnStep = (step, result) => Console.WriteLine($"Step {step}: {result.Actions.Count} actions");
        opts.StepDelayMs = 500;
    });

// Start the agent
var result = await loop.StartAsync("Open Calculator and compute 15 * 7");

Console.WriteLine($"Task finished: {result.Finished}");
```

## API Reference

### AgiClient

The main entry point for the SDK.

```csharp
// Constructor options
var client = new AgiClient(new AgiClientOptions
{
    ApiKey = "your-api-key",           // Or set AGI_API_KEY env var
    BaseUrl = "https://api.agi.tech",  // Default API URL
    Timeout = TimeSpan.FromSeconds(60), // Request timeout
    MaxRetries = 3                      // Retry count for transient failures
});

// Create sessions
await using var session = await client.SessionAsync("agi-0", options);
await using var desktopSession = await client.DesktopSessionAsync("agi-0", options);

// Low-level access
client.Sessions.CreateAsync(...)
client.Sessions.ListAsync()
client.Sessions.GetAsync(sessionId)
client.Sessions.DeleteAsync(sessionId)
```

### SessionContext

High-level session management with automatic cleanup.

```csharp
await using var session = await client.SessionAsync();

// Run a complete task
var result = await session.RunTaskAsync("task description", options);

// Or control manually
await session.SendMessageAsync("message");
var status = await session.GetStatusAsync();
var messages = await session.GetMessagesAsync();
await session.PauseAsync();
await session.ResumeAsync();
await session.CancelAsync();

// Browser control
await session.NavigateAsync("https://example.com");
var screenshot = await session.ScreenshotAsync();

// Stream events (SSE)
await foreach (var evt in session.StreamEventsAsync())
{
    Console.WriteLine($"Event: {evt.Event}");
}
```

### AgentLoop (Desktop Mode)

Client-driven execution loop for desktop automation.

```csharp
var loop = new AgentLoop(new AgentLoopOptions
{
    Client = client,
    AgentUrl = session.AgentUrl!,
    SessionId = session.SessionId,
    CaptureScreenshot = async () => /* return base64 screenshot */,
    ExecuteActions = async (actions) => /* execute actions locally */,
    OnThinking = thinking => Console.WriteLine(thinking),
    OnAskUser = async question =>
    {
        Console.WriteLine($"Agent asks: {question}");
        return Console.ReadLine()!;
    },
    OnStep = (step, result) => Console.WriteLine($"Step {step}"),
    OnError = ex => Console.WriteLine($"Error: {ex.Message}"),
    StepDelayMs = 500,
    MaxSteps = 100  // 0 = unlimited
});

// Start the loop
var result = await loop.StartAsync("Initial task message");

// Control the loop
loop.Pause();
loop.Resume();
loop.Stop();

// Check state
if (loop.IsRunning) { }
if (loop.IsPaused) { }
if (loop.IsFinished) { }
Console.WriteLine($"Current step: {loop.CurrentStep}");
```

### Desktop Actions

Actions returned by the agent for desktop mode:

```csharp
public enum DesktopActionType
{
    Click,           // Click at x, y with optional click_type
    Type,            // Type text
    Scroll,          // Scroll in direction with amount
    Hotkey,          // Press key combination (e.g., "Ctrl+a")
    Drag,            // Drag from start_x, start_y to x, y
    Wait,            // Wait for duration seconds
    Finished,        // Task is complete
    AwaitUserInput   // Agent needs user input
}

// Example action handling
foreach (var action in response.Actions)
{
    switch (action.Type)
    {
        case DesktopActionType.Click:
            var x = action.X!.Value;
            var y = action.Y!.Value;
            var clickType = action.ClickType ?? ClickType.Left;
            break;

        case DesktopActionType.Type:
            var text = action.Text!;
            break;

        case DesktopActionType.Scroll:
            var direction = action.Direction!.Value;
            var amount = action.Amount ?? 3;
            break;

        case DesktopActionType.Hotkey:
            var key = action.Key!; // e.g., "Ctrl+Shift+N"
            break;

        case DesktopActionType.Drag:
            var startX = action.StartX!.Value;
            var startY = action.StartY!.Value;
            var endX = action.X!.Value;
            var endY = action.Y!.Value;
            break;

        case DesktopActionType.Wait:
            var seconds = action.Duration!.Value;
            break;
    }
}
```

### Error Handling

The SDK provides typed exceptions for different error scenarios:

```csharp
try
{
    await using var session = await client.SessionAsync();
    var result = await session.RunTaskAsync("...");
}
catch (AuthenticationException ex)
{
    // Invalid or missing API key (401)
}
catch (NotFoundException ex)
{
    // Resource not found (404)
}
catch (PermissionException ex)
{
    // Permission denied (403)
}
catch (RateLimitException ex)
{
    // Rate limit exceeded (429)
    if (ex.RetryAfter.HasValue)
        await Task.Delay(ex.RetryAfter.Value);
}
catch (ValidationException ex)
{
    // Validation failed (422)
    foreach (var (field, errors) in ex.Errors ?? new())
        Console.WriteLine($"{field}: {string.Join(", ", errors)}");
}
catch (ApiException ex)
{
    // Server error (5xx)
}
catch (AgentExecutionException ex)
{
    // Agent failed during execution
    Console.WriteLine($"Session: {ex.SessionId}, Step: {ex.Step}");
}
catch (TimeoutException ex)
{
    // Operation timed out
}
catch (ConnectionException ex)
{
    // Network error
}
```

## Session Types

| Type | Description | Use Case |
|------|-------------|----------|
| `ManagedCdp` | API manages the browser | Standard browser automation |
| `ExternalCdp` | Connect to existing browser via CDP | Custom browser setup |
| `Desktop` | Client-driven execution | Desktop/mobile automation |

```csharp
// Server-driven (default)
await using var session = await client.SessionAsync();

// Desktop mode
await using var desktopSession = await client.DesktopSessionAsync();

// Or explicitly
await using var session = await client.SessionAsync("agi-0", new SessionCreateOptions
{
    AgentSessionType = AgentSessionType.Desktop
});
```

## Configuration

### Environment Variables

- `AGI_API_KEY` - API key for authentication

### Client Options

```csharp
var client = new AgiClient(new AgiClientOptions
{
    ApiKey = "sk-...",                     // API key
    BaseUrl = "https://api.agi.tech",      // API base URL
    Timeout = TimeSpan.FromSeconds(120),   // Request timeout
    MaxRetries = 5                          // Max retry attempts
});
```

### Session Options

```csharp
var options = new SessionCreateOptions
{
    MaxSteps = 100,                              // Max agent steps
    WebhookUrl = "https://your-webhook.com",    // Status webhook
    Goal = "Find flights",                       // Initial goal
    AgentSessionType = AgentSessionType.Desktop, // Session type
    RestoreFromEnvironmentId = "env-123",        // Restore snapshot
    CdpWsUrl = "ws://localhost:9222",           // External CDP
    Model = "gpt-4"                              // Model selection
};
```

## Demo App

An interactive Avalonia desktop GUI that showcases all SDK capabilities — sessions, SSE streaming, screenshots, pause/resume/cancel, and quick action cards.

**[agi-inc/demo-csharp-avalonia](https://github.com/agi-inc/demo-csharp-avalonia)**

```bash
git clone https://github.com/agi-inc/demo-csharp-avalonia.git && cd demo-csharp-avalonia
AGI_API_KEY=your-key dotnet run
```

## License

MIT
