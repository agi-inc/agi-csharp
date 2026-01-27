using Agi;
using Agi.Types;
using System.Text.Json;

// Example demonstrating SSE event streaming

Console.WriteLine("AGI C# SDK - Streaming Example");
Console.WriteLine("==============================\n");

using var client = new AgiClient();

// Create a session
Console.WriteLine("Creating session...");
await using var session = await client.SessionAsync();

Console.WriteLine($"Session created: {session.SessionId}");
Console.WriteLine();

// Start a task (don't await completion)
Console.WriteLine("Starting task...");
await session.SendMessageAsync(
    "Go to wikipedia.org and find information about artificial intelligence",
    new SendMessageOptions { StartUrl = "https://wikipedia.org" });

Console.WriteLine("Streaming events...\n");

// Stream events as they happen
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    await foreach (var evt in session.StreamEventsAsync(cancellationToken: cts.Token))
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        switch (evt.Event)
        {
            case EventType.Step:
                Console.WriteLine($"[{timestamp}] STEP");
                Console.WriteLine($"  Data: {evt.Data}");
                break;

            case EventType.Thought:
                var thought = evt.Data.GetProperty("content").GetString();
                Console.WriteLine($"[{timestamp}] THOUGHT: {thought}");
                break;

            case EventType.Question:
                var question = evt.Data.GetProperty("content").GetString();
                Console.WriteLine($"[{timestamp}] QUESTION: {question}");
                // In a real app, you'd get user input and send it back
                break;

            case EventType.Log:
                var log = evt.Data.GetProperty("content").GetString();
                Console.WriteLine($"[{timestamp}] LOG: {log}");
                break;

            case EventType.Paused:
                Console.WriteLine($"[{timestamp}] PAUSED");
                break;

            case EventType.Resumed:
                Console.WriteLine($"[{timestamp}] RESUMED");
                break;

            case EventType.Heartbeat:
                Console.WriteLine($"[{timestamp}] HEARTBEAT");
                break;

            case EventType.Error:
                var error = evt.Data.GetProperty("error").GetString();
                Console.WriteLine($"[{timestamp}] ERROR: {error}");
                break;

            case EventType.Done:
                Console.WriteLine($"[{timestamp}] DONE");
                Console.WriteLine("\nTask completed!");
                return; // Exit the loop

            default:
                Console.WriteLine($"[{timestamp}] {evt.Event}: {evt.Data}");
                break;
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nStreaming timed out.");
}

Console.WriteLine("\nSession cleaned up.");
