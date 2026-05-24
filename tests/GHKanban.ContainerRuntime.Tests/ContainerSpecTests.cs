using GHKanban.ContainerRuntime;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class ContainerSpecTests
{
    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var a = new ContainerSpec(
            Image: "ghcr.io/x/agent:0.1",
            Mounts: new[] { new ContainerMount("/host/foo", "/container/foo", ReadOnly: true) },
            Env: new Dictionary<string, string> { ["KEY"] = "value" },
            Labels: new Dictionary<string, string> { ["ghkanban"] = "true" },
            Timeout: TimeSpan.FromSeconds(60),
            CpuLimit: 1.0,
            MemoryBytes: 512L * 1024 * 1024);
        var b = a with { };
        Assert.Equal(a, b);
    }

    [Fact]
    public void RunResult_includes_exit_stdout_stderr()
    {
        var r = new ContainerRunResult(ExitCode: 0, Stdout: "ok", Stderr: null, TimedOut: false);
        Assert.Equal(0, r.ExitCode);
        Assert.False(r.TimedOut);
    }
}
