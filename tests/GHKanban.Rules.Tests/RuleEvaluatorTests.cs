using GHKanban.Core.Models;
using GHKanban.Rules;
using Xunit;

namespace GHKanban.Rules.Tests;

public class RuleEvaluatorTests
{
    private static IssueView Issue(
        string repo = "owner/repo", int number = 1, IssueState state = IssueState.Open,
        string[]? labels = null, string[]? assignees = null, string? milestone = null,
        int ageDays = 0)
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        return new IssueView(repo, number, "t", state,
            labels ?? [], assignees ?? [], milestone,
            CreatedAt: now.AddDays(-ageDays), UpdatedAt: now, HtmlUrl: "");
    }

    private static readonly DateTimeOffset _now = new(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Me = "myself";

    [Fact]
    public void HasLabel_matches_when_present()
        => Assert.True(Eval("has-label(\"bug\")", Issue(labels: ["bug"])));

    [Fact]
    public void HasLabel_does_not_match_when_absent()
        => Assert.False(Eval("has-label(\"bug\")", Issue(labels: ["feature"])));

    [Fact]
    public void AssigneeEquals_matches_case_insensitive()
        => Assert.True(Eval("assignee == \"Alice\"", Issue(assignees: ["alice"])));

    [Fact]
    public void AssigneeOfMine_matches_current_user()
        => Assert.True(Eval("assignee-of-mine", Issue(assignees: [Me])));

    [Fact]
    public void StateEquals_matches()
    {
        Assert.True(Eval("state == \"open\"", Issue(state: IssueState.Open)));
        Assert.True(Eval("state == \"closed\"", Issue(state: IssueState.Closed)));
        Assert.False(Eval("state == \"open\"", Issue(state: IssueState.Closed)));
    }

    [Fact]
    public void AgeDays_comparisons_work()
    {
        Assert.True(Eval("age-days > 5", Issue(ageDays: 10)));
        Assert.False(Eval("age-days > 5", Issue(ageDays: 3)));
        Assert.True(Eval("age-days < 5", Issue(ageDays: 3)));
    }

    [Fact]
    public void And_Or_Not_compose()
    {
        var i = Issue(labels: ["bug", "urgent"]);
        Assert.True(Eval("has-label(\"bug\") and has-label(\"urgent\")", i));
        Assert.True(Eval("has-label(\"bug\") or has-label(\"missing\")", i));
        Assert.False(Eval("not has-label(\"bug\")", i));
    }

    private static bool Eval(string rule, IssueView issue)
        => new RuleEvaluator(_now, Me).Evaluate(RuleParser.Parse(rule), issue);
}
