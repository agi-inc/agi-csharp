using Agi;
using Agi.Types;

// Desktop mode example demonstrating client-driven automation
// This example shows the structure but requires platform-specific
// screen capture and input simulation libraries to run.

Console.WriteLine("AGI C# SDK - Desktop Mode Example");
Console.WriteLine("==================================\n");

// Create client
using var client = new AgiClient();

// Create a desktop session
Console.WriteLine("Creating desktop session...");
await using var session = await client.DesktopSessionAsync();

Console.WriteLine($"Session created: {session.SessionId}");
Console.WriteLine($"Agent URL: {session.AgentUrl}");
Console.WriteLine();

// Create the agent loop with callbacks
var loop = session.CreateAgentLoop(
    client,
    captureScreenshot: CaptureScreenshotAsync,
    executeActions: ExecuteActionsAsync,
    configure: opts =>
    {
        opts.OnThinking = thinking =>
        {
            Console.WriteLine($"\n[Thinking] {thinking}");
        };

        opts.OnStep = (step, result) =>
        {
            Console.WriteLine($"\n[Step {step}]");
            Console.WriteLine($"  Actions: {result.Actions.Count}");
            Console.WriteLine($"  Finished: {result.Finished}");
            if (result.AskUser != null)
                Console.WriteLine($"  Question: {result.AskUser}");
        };

        opts.OnAskUser = async question =>
        {
            Console.WriteLine($"\n[Agent asks] {question}");
            Console.Write("Your response: ");
            return Console.ReadLine() ?? "";
        };

        opts.OnError = ex =>
        {
            Console.WriteLine($"\n[Error] {ex.Message}");
        };

        opts.StepDelayMs = 1000;  // 1 second between steps
        opts.MaxSteps = 50;       // Limit steps for safety
    });

// Start the agent
Console.WriteLine("Starting agent loop...\n");

try
{
    var result = await loop.StartAsync("Open Notepad and type 'Hello from AGI!'");

    Console.WriteLine("\n==================================");
    Console.WriteLine("Task completed!");
    Console.WriteLine($"  Total steps: {loop.CurrentStep}");
    Console.WriteLine($"  Finished: {result.Finished}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nAgent loop was cancelled.");
}
catch (AgentExecutionException ex)
{
    Console.WriteLine($"\nAgent execution failed: {ex.Message}");
}

Console.WriteLine("\nSession cleaned up.");

// Placeholder implementation - replace with actual screen capture
async Task<string> CaptureScreenshotAsync()
{
    // In a real implementation, use a library like:
    // - Windows: System.Drawing, Windows.Graphics.Capture
    // - Cross-platform: ScreenCapture.NET, ImageSharp
    Console.WriteLine("  [Capturing screenshot...]");

    // Return a placeholder - in production, return actual base64 screenshot
    await Task.Delay(100);

    // Example of what a real implementation might look like:
    // using var bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
    //                               Screen.PrimaryScreen.Bounds.Height);
    // using var graphics = Graphics.FromImage(bitmap);
    // graphics.CopyFromScreen(Point.Empty, Point.Empty, bitmap.Size);
    // using var ms = new MemoryStream();
    // bitmap.Save(ms, ImageFormat.Png);
    // return Convert.ToBase64String(ms.ToArray());

    throw new NotImplementedException(
        "Replace with actual screen capture implementation");
}

// Placeholder implementation - replace with actual input simulation
async Task ExecuteActionsAsync(IReadOnlyList<DesktopAction> actions)
{
    // In a real implementation, use a library like:
    // - Windows: InputSimulator, Windows.UI.Input.Preview.Injection
    // - Cross-platform: SharpHook

    foreach (var action in actions)
    {
        Console.WriteLine($"  [Executing] {action.Type}");

        switch (action.Type)
        {
            case DesktopActionType.Click:
                Console.WriteLine($"    Click at ({action.X}, {action.Y})");
                // Example: InputSimulator.Mouse.MoveMouseTo(x, y);
                //          InputSimulator.Mouse.LeftButtonClick();
                break;

            case DesktopActionType.Type:
                Console.WriteLine($"    Type: \"{action.Text}\"");
                // Example: InputSimulator.Keyboard.TextEntry(action.Text);
                break;

            case DesktopActionType.Scroll:
                Console.WriteLine($"    Scroll {action.Direction} by {action.Amount}");
                // Example: InputSimulator.Mouse.VerticalScroll(amount);
                break;

            case DesktopActionType.Hotkey:
                Console.WriteLine($"    Hotkey: {action.Key}");
                // Example: Parse "Ctrl+a" and simulate key combination
                break;

            case DesktopActionType.Drag:
                Console.WriteLine($"    Drag from ({action.StartX}, {action.StartY}) to ({action.X}, {action.Y})");
                break;

            case DesktopActionType.Wait:
                Console.WriteLine($"    Wait {action.Duration}s");
                await Task.Delay(TimeSpan.FromSeconds(action.Duration ?? 1));
                break;

            case DesktopActionType.Finished:
                Console.WriteLine("    Task marked as finished");
                break;

            case DesktopActionType.AwaitUserInput:
                Console.WriteLine("    Waiting for user input...");
                break;
        }

        await Task.Delay(100); // Small delay between actions
    }
}
