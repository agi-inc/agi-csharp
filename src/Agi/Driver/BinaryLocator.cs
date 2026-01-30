using System.Runtime.InteropServices;

namespace Agi.Driver;

/// <summary>
/// Platform identifier for binary selection.
/// </summary>
public enum PlatformId
{
    DarwinArm64,
    DarwinX64,
    LinuxX64,
    WindowsX64
}

/// <summary>
/// Locates the agi-driver binary for the current platform.
/// </summary>
public static class BinaryLocator
{
    /// <summary>
    /// Get the current platform identifier.
    /// </summary>
    public static PlatformId GetPlatformId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? PlatformId.DarwinArm64
                : PlatformId.DarwinX64;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return PlatformId.LinuxX64;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return PlatformId.WindowsX64;
        }

        throw new PlatformNotSupportedException(
            $"Unsupported platform: {RuntimeInformation.OSDescription}");
    }

    /// <summary>
    /// Get the binary filename for the given platform.
    /// </summary>
    public static string GetBinaryFilename(PlatformId? platformId = null)
    {
        var id = platformId ?? GetPlatformId();
        return id == PlatformId.WindowsX64 ? "agi-driver.exe" : "agi-driver";
    }

    /// <summary>
    /// Get the runtime identifier string for the given platform.
    /// </summary>
    public static string GetRuntimeIdentifier(PlatformId? platformId = null)
    {
        var id = platformId ?? GetPlatformId();
        return id switch
        {
            PlatformId.DarwinArm64 => "osx-arm64",
            PlatformId.DarwinX64 => "osx-x64",
            PlatformId.LinuxX64 => "linux-x64",
            PlatformId.WindowsX64 => "win-x64",
            _ => throw new ArgumentException($"Unknown platform: {id}")
        };
    }

    /// <summary>
    /// Find the agi-driver binary path.
    /// </summary>
    public static string FindBinaryPath()
    {
        var platformId = GetPlatformId();
        var filename = GetBinaryFilename(platformId);
        var runtimeId = GetRuntimeIdentifier(platformId);

        var searchPaths = new List<string>();

        // 1. NuGet runtime-specific native directory
        var assemblyDir = Path.GetDirectoryName(typeof(BinaryLocator).Assembly.Location);
        if (assemblyDir != null)
        {
            searchPaths.Add(Path.Combine(assemblyDir, "runtimes", runtimeId, "native", filename));
            searchPaths.Add(Path.Combine(assemblyDir, filename));
        }

        // 2. Current directory
        searchPaths.Add(Path.Combine(Environment.CurrentDirectory, filename));

        // 3. PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(Path.PathSeparator);
        foreach (var dir in pathDirs)
        {
            if (!string.IsNullOrEmpty(dir))
            {
                searchPaths.Add(Path.Combine(dir, filename));
            }
        }

        // Search for the binary
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Could not find agi-driver binary for {platformId}. " +
            $"Searched: {string.Join(", ", searchPaths.Take(5))}...");
    }

    /// <summary>
    /// Check if the binary is available.
    /// </summary>
    public static bool IsBinaryAvailable()
    {
        try
        {
            FindBinaryPath();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
