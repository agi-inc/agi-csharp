using Agi;
using Agi.Types;

Console.WriteLine("AGI C# SDK - Smoke Test");
Console.WriteLine("=======================\n");

// Get config from environment or arguments
var apiKey = Environment.GetEnvironmentVariable("AGI_API_KEY")
    ?? Environment.GetEnvironmentVariable("USER_API_KEY")
    ?? throw new Exception("Set AGI_API_KEY or USER_API_KEY environment variable");

var baseUrl = Environment.GetEnvironmentVariable("AGI_API_BASE_URL")
    ?? "https://api.agi.tech";

Console.WriteLine($"Base URL: {baseUrl}");
Console.WriteLine($"API Key: {apiKey[..8]}...{apiKey[^4..]}\n");

try
{
    // Create client
    using var client = new AgiClient(new AgiClientOptions
    {
        ApiKey = apiKey,
        BaseUrl = baseUrl,
        Timeout = TimeSpan.FromSeconds(30)
    });

    // Test 1: List sessions
    Console.WriteLine("Test 1: List sessions...");
    var sessions = await client.Sessions.ListAsync();
    Console.WriteLine($"  ✓ Found {sessions.Count} existing session(s)\n");

    // Test 2: Create a session
    Console.WriteLine("Test 2: Create session...");
    var session = await client.Sessions.CreateAsync("agi-0");
    Console.WriteLine($"  ✓ Created session: {session.SessionId}");
    Console.WriteLine($"    Status: {session.Status}");
    Console.WriteLine($"    VNC URL: {session.VncUrl ?? "N/A"}\n");

    // Test 3: Get session
    Console.WriteLine("Test 3: Get session...");
    var retrieved = await client.Sessions.GetAsync(session.SessionId);
    Console.WriteLine($"  ✓ Retrieved session: {retrieved.SessionId}\n");

    // Test 4: Get status
    Console.WriteLine("Test 4: Get status...");
    var status = await client.Sessions.GetStatusAsync(session.SessionId);
    Console.WriteLine($"  ✓ Status: {status.Status}\n");

    // Test 5: Delete session
    Console.WriteLine("Test 5: Delete session...");
    var deleteResult = await client.Sessions.DeleteAsync(session.SessionId);
    Console.WriteLine($"  ✓ Deleted: {deleteResult.Success}\n");

    // Test 6: SessionContext with auto-cleanup
    Console.WriteLine("Test 6: SessionContext (auto-cleanup)...");
    await using (var ctx = await client.SessionAsync())
    {
        Console.WriteLine($"  ✓ Created context session: {ctx.SessionId}");
        var ctxStatus = await ctx.GetStatusAsync();
        Console.WriteLine($"    Status: {ctxStatus.Status}");
    }
    Console.WriteLine("  ✓ Session auto-deleted on dispose\n");

    Console.WriteLine("=======================");
    Console.WriteLine("All tests passed! ✓");
}
catch (AuthenticationException ex)
{
    Console.WriteLine($"\n✗ Authentication failed: {ex.Message}");
    Console.WriteLine("  Check your API key");
    Environment.Exit(1);
}
catch (AgiException ex)
{
    Console.WriteLine($"\n✗ API error: {ex.Message}");
    Console.WriteLine($"  Status code: {ex.StatusCode}");
    Console.WriteLine($"  Response: {ex.ResponseContent}");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Error: {ex.Message}");
    Console.WriteLine($"  {ex.GetType().Name}");
    Environment.Exit(1);
}
