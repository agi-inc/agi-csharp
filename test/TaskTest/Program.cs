using Agi;
using Agi.Types;

Console.WriteLine("AGI C# SDK - Task Test");
Console.WriteLine("======================\n");

var apiKey = Environment.GetEnvironmentVariable("AGI_API_KEY")
    ?? Environment.GetEnvironmentVariable("USER_API_KEY")
    ?? throw new Exception("Set AGI_API_KEY or USER_API_KEY");

var baseUrl = Environment.GetEnvironmentVariable("AGI_API_BASE_URL")
    ?? "https://api.agi.tech";

using var client = new AgiClient(new AgiClientOptions
{
    ApiKey = apiKey,
    BaseUrl = baseUrl,
    Timeout = TimeSpan.FromSeconds(60)
});

Console.WriteLine("Creating session...");
await using var session = await client.SessionAsync();

Console.WriteLine($"Session: {session.SessionId}");
Console.WriteLine($"VNC URL: {session.VncUrl}\n");
Console.WriteLine("Watch the browser at the VNC URL above!\n");

Console.WriteLine("Sending task: 'Go to amazon.com and search for snacks'\n");

try
{
    var result = await session.RunTaskAsync(
        "Go to amazon.com and search for snacks. Tell me the name and price of the first 3 results.",
        new RunTaskOptions
        {
            StartUrl = "https://amazon.com",
            PollInterval = TimeSpan.FromSeconds(3),
            Timeout = TimeSpan.FromMinutes(5),
            OnStatusChange = status => Console.WriteLine($"[Status] {status}"),
            OnMessage = msg =>
            {
                var content = msg.GetContentAsString();
                if (content?.Length > 200) content = content[..200] + "...";
                Console.WriteLine($"[{msg.Type}] {content}");
            }
        });

    Console.WriteLine("\n======================");
    Console.WriteLine($"Task completed in {result.Metadata.Duration:F1}s");
    Console.WriteLine($"Steps: {result.Metadata.Steps}");
    Console.WriteLine($"Success: {result.Metadata.Success}");
}
catch (Exception ex)
{
    Console.WriteLine($"\nError: {ex.Message}");
}
