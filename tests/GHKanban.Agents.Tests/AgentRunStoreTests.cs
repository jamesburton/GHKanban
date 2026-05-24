using GHKanban.Agents;
using GHKanban.Core.Models;
using GHKanban.Sync;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GHKanban.Agents.Tests;

public class AgentRunStoreTests
{
    [Fact]
    public async Task RecordsAndListsRuns()
    {
        var conn = OpenInMemory();
        SqliteSchema.Apply(conn);
        var store = new AgentRunStore(conn);

        await store.RecordAsync(new AgentRunRecord(
            AgentName: "Stub", TriggerEvent: "issue.labeled",
            Repo: "owner/repo", IssueNumber: 1,
            StartedAt: DateTimeOffset.UtcNow, FinishedAt: DateTimeOffset.UtcNow,
            Status: AgentRunStatus.Success, Output: "ok", Error: null),
            TestContext.Current.CancellationToken);

        var recent = (await store.GetRecentAsync(limit: 10, TestContext.Current.CancellationToken)).ToList();
        Assert.Single(recent);
        Assert.Equal("Stub", recent[0].AgentName);
    }

    private static SqliteConnection OpenInMemory()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }
}
