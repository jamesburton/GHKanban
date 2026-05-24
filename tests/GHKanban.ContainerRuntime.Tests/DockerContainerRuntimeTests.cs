using GHKanban.ContainerRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class DockerContainerRuntimeTests
{
    private static bool DockerAvailable()
    {
        try
        {
            using var client = new Docker.DotNet.DockerClientConfiguration(DockerSocketLocator.Resolve()).CreateClient();
            client.System.PingAsync().GetAwaiter().GetResult();
            return true;
        }
        catch { return false; }
    }

    [Fact]
    public async Task Runs_hello_world_container_returns_exit_zero()
    {
        if (!DockerAvailable()) return;  // skip when no Docker daemon

        var runtime = new DockerContainerRuntime(NullLogger<DockerContainerRuntime>.Instance);
        var spec = new ContainerSpec(
            Image: "hello-world",
            Mounts: Array.Empty<ContainerMount>(),
            Env: new Dictionary<string, string>(),
            Labels: new Dictionary<string, string> { ["ghkanban"] = "test", ["ghkanban.test"] = "smoke" },
            Timeout: TimeSpan.FromSeconds(30),
            CpuLimit: 1.0,
            MemoryBytes: 64L * 1024 * 1024);

        var result = await runtime.RunAsync(spec, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("Hello from Docker", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_times_out_when_container_hangs()
    {
        if (!DockerAvailable()) return;

        var runtime = new DockerContainerRuntime(NullLogger<DockerContainerRuntime>.Instance);

        // nginx:alpine blocks indefinitely by default, making it a reliable timeout target.
        // alpine with no Cmd exits immediately (sh sees EOF), so would not trigger the timeout.
        var spec = new ContainerSpec(
            Image: "nginx:alpine",
            Mounts: Array.Empty<ContainerMount>(),
            Env: new Dictionary<string, string>(),
            Labels: new Dictionary<string, string> { ["ghkanban"] = "test" },
            Timeout: TimeSpan.FromSeconds(2),
            CpuLimit: 1.0,
            MemoryBytes: 64L * 1024 * 1024);

        var result = await runtime.RunAsync(spec, TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut, "expected timeout to be set");
    }
}
