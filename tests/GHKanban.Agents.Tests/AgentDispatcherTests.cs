using GHKanban.Agents;
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using GHKanban.Sync;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Agents.Tests;

public class AgentDispatcherTests
{
    [Fact]
    public async Task DispatchesMatchingAgentAndRecordsRun()
    {
        var agent = Substitute.For<IGHKanbanAgent>();
        agent.Name.Returns("Stub");
        agent.TriggerAsync(Arg.Any<IssueContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentRunResult(AgentRunStatus.Success, "ok", null));

        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        SqliteSchema.Apply(conn);
        var store = new AgentRunStore(conn);

        var dispatcher = new AgentDispatcher(
            new Dictionary<string, IGHKanbanAgent>(StringComparer.OrdinalIgnoreCase) { ["stub"] = agent },
            store,
            currentUser: "me",
            NullLogger<AgentDispatcher>.Instance);

        var ev = new IssueEvent(
            EventType.IssueLabeled,
            new IssueView("owner/repo", 1, "t", IssueState.Open, ["ai-pls"], [], null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ""),
            "ai-pls", null, null, DateTimeOffset.UtcNow);

        var agentCfg = new AgentConfig("stub", "Stub", "Test",
            new[] { new TriggerSpec("issue.labeled", "has-label(\"ai-pls\")") });

        await dispatcher.DispatchAsync(ev, new[] { agentCfg }, TestContext.Current.CancellationToken);

        await agent.Received(1).TriggerAsync(Arg.Any<IssueContext>(), Arg.Any<CancellationToken>());
        Assert.Single(await store.GetRecentAsync(10, TestContext.Current.CancellationToken));
    }
}
