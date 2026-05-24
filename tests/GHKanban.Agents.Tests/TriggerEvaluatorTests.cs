using GHKanban.Agents;
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Agents.Tests;

public class TriggerEvaluatorTests
{
    [Fact]
    public void MatchesLabeledTriggerWithMatchingLabel()
    {
        var trigger = new TriggerSpec(On: "issue.labeled", When: "has-label(\"ai-pls\")");
        var issue = Issue(labels: ["ai-pls"]);
        var ev = new IssueEvent(EventType.IssueLabeled, issue, "ai-pls", null, null, DateTimeOffset.UtcNow);
        Assert.True(TriggerEvaluator.Matches(trigger, ev, currentUser: "me"));
    }

    [Fact]
    public void DoesNotMatchWhenEventTypeDiffers()
    {
        var trigger = new TriggerSpec(On: "issue.assigned", When: "has-label(\"ai-pls\")");
        var ev = new IssueEvent(EventType.IssueLabeled, Issue(labels: ["ai-pls"]), "ai-pls", null, null, DateTimeOffset.UtcNow);
        Assert.False(TriggerEvaluator.Matches(trigger, ev, "me"));
    }

    [Fact]
    public void EmptyWhenIsAlwaysTrue()
    {
        var trigger = new TriggerSpec(On: "issue.opened", When: "true");
        var ev = new IssueEvent(EventType.IssueOpened, Issue(), null, null, null, DateTimeOffset.UtcNow);

        // "true" is a special-cased always-match; the parser would reject it as an unknown identifier.
        // The evaluator treats null/empty/literal "true" as always-true.
        Assert.True(TriggerEvaluator.Matches(trigger, ev, "me"));
    }

    private static IssueView Issue(string[]? labels = null) =>
        new("owner/repo", 1, "t", IssueState.Open, labels ?? [], [], null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "");
}
