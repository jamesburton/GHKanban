using GHKanban.Agents;
using GHKanban.ContainerRuntime;
using GHKanban.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Agents.Tests;

public class ContainerAgentTests
{
    [Fact]
    public async Task Builds_correct_ContainerSpec_and_records_success()
    {
        var runtime = Substitute.For<IContainerRuntime>();
        runtime.RunAsync(Arg.Any<ContainerSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ContainerRunResult(ExitCode: 0,
                Stdout: "{\"event\":\"complete\",\"comment_url\":\"https://github.com/x/y/issues/1#issuecomment-1\"}",
                Stderr: null, TimedOut: false));

        var cfg = new AgentConfig(
            Id: "summariser",
            Name: "Summariser",
            Implementation: "container",
            Triggers: new[] { new TriggerSpec("issue.opened", "true") },
            Container: new ContainerAgentSpec(
                Image: "ghcr.io/x/agent:1",
                Llm: new ContainerLlmSpec("none", null, null),
                Prompt: new ContainerPromptSpec(null, "hello"),
                Tools: new[] { "github.post-comment" },
                Timeout: TimeSpan.FromSeconds(30),
                CpuLimit: 1.0,
                MemoryBytes: 256L * 1024 * 1024));

        var dirs = new ContainerAgentDirs(
            ConfigRoot: Path.GetTempPath(),
            RunsRoot: Path.Combine(Path.GetTempPath(), $"runs-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(dirs.RunsRoot);

        try
        {
            var agent = new ContainerAgent("Summariser", cfg, runtime, dirs, NullLogger<ContainerAgent>.Instance);
            var ctx = new IssueContext(
                Issue: new IssueView("x/y", 1, "t", IssueState.Open, [], [], null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "https://github.com/x/y/issues/1"),
                TriggerEvent: "issue.opened",
                MatchingRule: "true",
                AgentName: "Summariser");

            var result = await agent.TriggerAsync(ctx, TestContext.Current.CancellationToken);

            Assert.Equal(AgentRunStatus.Success, result.Status);
            await runtime.Received(1).RunAsync(Arg.Is<ContainerSpec>(s =>
                s.Image == "ghcr.io/x/agent:1" &&
                s.Labels.ContainsKey("ghkanban") &&
                s.Mounts.Any(m => m.ContainerPath == "/event.json")
            ), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(dirs.RunsRoot)) Directory.Delete(dirs.RunsRoot, true);
        }
    }

    [Fact]
    public async Task Records_failed_when_container_exits_nonzero()
    {
        var runtime = Substitute.For<IContainerRuntime>();
        runtime.RunAsync(Arg.Any<ContainerSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ContainerRunResult(ExitCode: 1, Stdout: "", Stderr: "boom", TimedOut: false));

        var cfg = new AgentConfig("x", "X", "container",
            new[] { new TriggerSpec("issue.opened", "true") },
            Container: new ContainerAgentSpec("img", new("none", null, null), new(null, "hi"),
                new[] { "github.post-comment" }, TimeSpan.FromSeconds(10), 1.0, 256L * 1024 * 1024));

        var dirs = new ContainerAgentDirs(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), $"runs-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(dirs.RunsRoot);
        try
        {
            var agent = new ContainerAgent("X", cfg, runtime, dirs, NullLogger<ContainerAgent>.Instance);
            var ctx = new IssueContext(
                Issue: new IssueView("x/y", 1, "t", IssueState.Open, [], [], null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ""),
                TriggerEvent: "issue.opened", MatchingRule: "true", AgentName: "X");

            var result = await agent.TriggerAsync(ctx, TestContext.Current.CancellationToken);

            Assert.Equal(AgentRunStatus.Failed, result.Status);
            Assert.Contains("boom", result.Error);
        }
        finally { Directory.Delete(dirs.RunsRoot, true); }
    }
}
