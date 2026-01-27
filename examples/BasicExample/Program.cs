using Agi;
using Agi.Types;

// Basic example demonstrating server-driven browser automation

Console.WriteLine("AGI C# SDK - Basic Example");
Console.WriteLine("==========================\n");

// Create client (reads API key from AGI_API_KEY environment variable)
using var client = new AgiClient();

// Create a session with automatic cleanup
Console.WriteLine("Creating session...");
await using var session = await client.SessionAsync();

Console.WriteLine($"Session created: {session.SessionId}");
Console.WriteLine($"VNC URL: {session.VncUrl ?? "N/A"}");
Console.WriteLine();

// Run a task with progress tracking
Console.WriteLine("Running task...\n");

try
{
    var result = await session.RunTaskAsync(
        "Go to google.com and search for 'AGI automation'",
        new RunTaskOptions
        {
            StartUrl = "https://google.com",
            PollInterval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromMinutes(5),
            OnStatusChange = status =>
            {
                Console.WriteLine($"[Status] {status}");
            },
            OnMessage = msg =>
            {
                var content = msg.GetContentAsString();
                var prefix = msg.Type switch
                {
                    MessageType.Thought => "[Thought]",
                    MessageType.Question => "[Question]",
                    MessageType.Done => "[Done]",
                    MessageType.Error => "[Error]",
                    MessageType.Log => "[Log]",
                    _ => $"[{msg.Type}]"
                };
                Console.WriteLine($"{prefix} {content}");
            }
        });

    Console.WriteLine("\n==========================");
    Console.WriteLine("Task completed!");
    Console.WriteLine($"  Duration: {result.Metadata.Duration:F1}s");
    Console.WriteLine($"  Steps: {result.Metadata.Steps}");
    Console.WriteLine($"  Success: {result.Metadata.Success}");
}
catch (AgentExecutionException ex)
{
    Console.WriteLine($"\nAgent execution failed: {ex.Message}");
    Console.WriteLine($"  Session: {ex.SessionId}");
    Console.WriteLine($"  Step: {ex.Step}");
}
catch (Agi.TimeoutException ex)
{
    Console.WriteLine($"\nTask timed out: {ex.Message}");
}
catch (AgiException ex)
{
    Console.WriteLine($"\nAPI error: {ex.Message}");
    Console.WriteLine($"  Status code: {ex.StatusCode}");
}

Console.WriteLine("\nSession will be cleaned up automatically.");
