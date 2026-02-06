/// <summary>
/// Integration tests for AgentDriver with real agi-driver binary.
///
/// Requires:
///   - agi-driver binary available on PATH or in standard locations
///   - ANTHROPIC_API_KEY: valid Anthropic API key
///
/// Spawns the real driver, communicates over JSON lines, and runs
/// a real task with the Anthropic API.
/// </summary>

using Agi.Driver;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";

if (!BinaryLocator.IsBinaryAvailable())
{
    Console.WriteLine("SKIP: agi-driver binary not found");
    return 0;
}

if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("SKIP: ANTHROPIC_API_KEY not set");
    return 0;
}

var passed = 0;
var failed = 0;

// Test 1: Full end-to-end local mode task
try
{
    Console.WriteLine("TEST: driver_local_mode...");

    var thinkingTexts = new List<string>();
    var states = new List<DriverState>();

    var driver = new AgentDriver(new DriverOptions
    {
        Mode = "local",
        Environment = new Dictionary<string, string>
        {
            ["ANTHROPIC_API_KEY"] = apiKey
        }
    });

    driver.OnThinking += text =>
    {
        thinkingTexts.Add(text);
        return Task.CompletedTask;
    };
    driver.OnStateChange += state => states.Add(state);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var result = await driver.StartAsync(
        goal: "Take a screenshot and describe what you see. Then finish.",
        mode: "local",
        cancellationToken: cts.Token);

    Assert(result.Success, "result.Success should be true");
    Assert(!string.IsNullOrEmpty(result.Summary), "result.Summary should be non-empty");
    Assert(thinkingTexts.Count > 0, "should have received thinking events");
    Assert(states.Contains(DriverState.Running), "should have entered running state");

    Console.WriteLine("  PASSED");
    passed++;
}
catch (Exception ex)
{
    Console.WriteLine($"  FAILED: {ex.Message}");
    failed++;
}

// Test 2: Protocol handshake and clean stop
try
{
    Console.WriteLine("TEST: driver_protocol_handshake...");

    var states = new List<DriverState>();
    var gotThinking = new TaskCompletionSource<bool>();

    var driver = new AgentDriver(new DriverOptions
    {
        Mode = "local",
        Environment = new Dictionary<string, string>
        {
            ["ANTHROPIC_API_KEY"] = apiKey
        }
    });

    driver.OnStateChange += state => states.Add(state);
    driver.OnThinking += _ =>
    {
        gotThinking.TrySetResult(true);
        return Task.CompletedTask;
    };

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    // Start in background and stop after first thinking event
    var resultTask = driver.StartAsync(
        goal: "Describe the screen",
        mode: "local",
        cancellationToken: cts.Token);

    // Wait for first thinking event
    await Task.WhenAny(gotThinking.Task, Task.Delay(TimeSpan.FromSeconds(30), cts.Token));
    Assert(gotThinking.Task.IsCompleted, "should have received thinking event");

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
