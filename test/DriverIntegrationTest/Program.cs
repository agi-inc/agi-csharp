/// <summary>
/// Integration tests for AgentDriver with real agi-driver binary.
///
/// Requires:
///   - agi-driver binary available on PATH or in standard locations
///   - AGI_API_KEY: valid AGI API key
///
/// Spawns the real driver, communicates over JSON lines, and runs
/// a real task via the AGI API.
/// </summary>

using Agi.Driver;

var apiKey = Environment.GetEnvironmentVariable("AGI_API_KEY") ?? "";

if (!BinaryLocator.IsBinaryAvailable())
{
    Console.WriteLine("SKIP: agi-driver binary not found");
    return 0;
}

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("SKIP: AGI_API_KEY not set");
    return 0;
}

var passed = 0;
var failed = 0;

// Test 1: Full end-to-end local mode task (client-desktop)
try
{
    Console.WriteLine("TEST: driver_local_mode (client-desktop)...");

    var states = new List<DriverState>();

    var driver = new AgentDriver(new DriverOptions
    {
        Mode = "local",
        Environment = new Dictionary<string, string>
        {
            ["AGI_API_KEY"] = apiKey
        }
    });

    driver.OnStateChange += state => states.Add(state);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    var result = await driver.StartAsync(
        goal: "Take a screenshot and describe what you see. Then finish.",
        mode: "local",
        cancellationToken: cts.Token);

    Assert(result.Success, "result.Success should be true");
    Assert(!string.IsNullOrEmpty(result.Summary), "result.Summary should be non-empty");
    // TODO: thinking events depend on API returning thinking text (not yet implemented)
    Assert(states.Contains(DriverState.Running), "should have entered running state");

    Console.WriteLine("  PASSED");
    passed++;
}
catch (Exception ex)
{
    Console.WriteLine($"  FAILED: {ex.Message}");
    failed++;
}

// Test 2: Remote mode task (client-managed-desktop)
try
{
    Console.WriteLine("TEST: driver_remote_mode (client-managed-desktop)...");

    var states = new List<DriverState>();
    var gotSessionCreated = false;

    var driver = new AgentDriver(new DriverOptions
    {
        Mode = "remote",
        EnvironmentType = "ubuntu-1",
        Environment = new Dictionary<string, string>
        {
            ["AGI_API_KEY"] = apiKey
        }
    });

    driver.OnStateChange += state => states.Add(state);
    driver.OnEvent += evt =>
    {
        if (evt is SessionCreatedEvent)
            gotSessionCreated = true;
    };

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
    var result = await driver.StartAsync(
        goal: "Take a screenshot and describe what you see. Then finish.",
        mode: "remote",
        cancellationToken: cts.Token);

    Assert(result.Success, "result.Success should be true");
    Assert(!string.IsNullOrEmpty(result.Summary), "result.Summary should be non-empty");
    Assert(gotSessionCreated, "should have received session_created event");
    Assert(states.Contains(DriverState.Running), "should have entered running state");

    Console.WriteLine("  PASSED");
    passed++;
}
catch (Exception ex)
{
    if (ex.Message.Contains("503") || ex.Message.Contains("entrypoint"))
    {
        Console.WriteLine("  SKIP: Remote environment unavailable (503)");
    }
    else
    {
        Console.WriteLine($"  FAILED: {ex.Message}");
        failed++;
    }
}

// Test 3: Protocol handshake and clean stop
try
{
    Console.WriteLine("TEST: driver_protocol_handshake...");

    var states = new List<DriverState>();
    var gotAction = new TaskCompletionSource<bool>();

    var driver = new AgentDriver(new DriverOptions
    {
        Mode = "local",
        Environment = new Dictionary<string, string>
        {
            ["AGI_API_KEY"] = apiKey
        }
    });

    driver.OnStateChange += state => states.Add(state);
    // Gate on first action event (always emitted) instead of thinking (not yet returned by API)
    driver.OnAction += _ =>
    {
        gotAction.TrySetResult(true);
        return Task.CompletedTask;
    };

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    // Start in background and stop after first action event
    var resultTask = driver.StartAsync(
        goal: "Describe the screen",
        mode: "local",
        cancellationToken: cts.Token);

    // Wait for first action event
    await Task.WhenAny(gotAction.Task, Task.Delay(TimeSpan.FromSeconds(30), cts.Token));
    Assert(gotAction.Task.IsCompleted, "should have received action event");

    await driver.StopAsync("test complete");

    // Task should resolve or throw without hanging
    try
    {
        await resultTask;
    }
    catch
    {
        // Stopped driver may throw; that's expected
    }

    Assert(states.Contains(DriverState.Running), "should have entered running state");

    Console.WriteLine("  PASSED");
    passed++;
}
catch (Exception ex)
{
    Console.WriteLine($"  FAILED: {ex.Message}");
    failed++;
}

Console.WriteLine($"\nResults: {passed} passed, {failed} failed");
return failed > 0 ? 1 : 0;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception($"Assertion failed: {message}");
}
