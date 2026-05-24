using GHKanban.ContainerRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class ContainerJanitorTests
{
    [Fact]
    public async Task CleansExitedContainersOlderThanThreshold()
    {
        var runtime = Substitute.For<IContainerRuntime>();
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var old = new ContainerHandle("a", "exited", now.AddMinutes(-45));
        var young = new ContainerHandle("b", "exited", now.AddMinutes(-5));
        var running = new ContainerHandle("c", "running", now.AddMinutes(-60));
        runtime.ListLabeledAsync("ghkanban", "true", Arg.Any<CancellationToken>())
            .Returns(new[] { old, young, running });

        await ContainerJanitor.SweepOnceAsync(runtime, now, threshold: TimeSpan.FromMinutes(30),
            NullLogger.Instance, TestContext.Current.CancellationToken);

        await runtime.Received(1).RemoveAsync("a", Arg.Any<CancellationToken>());
        await runtime.DidNotReceive().RemoveAsync("b", Arg.Any<CancellationToken>());
        await runtime.DidNotReceive().RemoveAsync("c", Arg.Any<CancellationToken>());
    }
}
