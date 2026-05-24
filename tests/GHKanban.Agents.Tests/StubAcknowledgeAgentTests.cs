using GHKanban.Agents;
using GHKanban.Core.Models;
using GHKanban.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Agents.Tests;

public class StubAcknowledgeAgentTests
{
    [Fact]
    public async Task PostsExpectedCommentFormat()
    {
        var writer = Substitute.For<IGitHubWriter>();
        var agent = new StubAcknowledgeAgent("My Agent", writer, NullLogger<StubAcknowledgeAgent>.Instance);

        var context = new IssueContext(
            Issue: new IssueView("owner/repo", 42, "t", IssueState.Open, [], [], null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "https://github.com/owner/repo/issues/42"),
            TriggerEvent: "issue.labeled",
            MatchingRule: "has-label(\"ai-pls\")",
            AgentName: "My Agent");

        var result = await agent.TriggerAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(AgentRunStatus.Success, result.Status);
        await writer.Received(1).PostCommentAsync(
            "owner/repo", 42,
            Arg.Is<string>(s =>
                s.Contains("My Agent") && s.Contains("issue.labeled") && s.Contains("has-label(\"ai-pls\")")),
            Arg.Any<CancellationToken>());
    }
}
