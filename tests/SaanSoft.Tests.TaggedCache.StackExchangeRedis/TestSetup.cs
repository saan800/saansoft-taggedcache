using System.Runtime.CompilerServices;

namespace SaanSoft.Tests.TaggedCache.StackExchangeRedis;

internal static class TestSetup
{
    // Runs at assembly load time — before TestcontainersSettings.ctor() accesses DOCKER_HOST.
    //
    // Docker Desktop's WSL2 integration sets DOCKER_HOST=npipe:////./pipe/docker_engine in the
    // shell environment so the Docker CLI can reach the Windows daemon. However, named pipes are
    // a Windows concept; the Linux Docker.DotNet client library used by Testcontainers cannot
    // connect via that URI. When running on Linux (including WSL2), we override to a Unix socket.
    [ModuleInitializer]
    internal static void EnsureDockerHost()
    {
        if (OperatingSystem.IsWindows())
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
                Environment.SetEnvironmentVariable("DOCKER_HOST", "npipe:////./pipe/docker_engine");

            return;
        }

        // On Linux / WSL2: if DOCKER_HOST is a Windows named pipe, override it with a Unix socket.
        var current = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? string.Empty;
        if (!current.StartsWith("npipe:", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(current))
        {
            return; // Already a sensible Linux endpoint; leave it alone.
        }

        // Try common Docker socket locations in order.
        var candidates = new[]
        {
            "/var/run/docker.sock",
            $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.docker/desktop/docker.sock",
            "/run/docker.sock",
        };

        var socket = Array.Find(candidates, File.Exists);
        if (socket != null)
            Environment.SetEnvironmentVariable("DOCKER_HOST", $"unix://{socket}");
    }
}
