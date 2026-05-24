using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Core.Tests;

public class IssueViewTests
{
    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var a = new IssueView(
            Repo: "owner/repo",
            Number: 42,
            Title: "Bug",
            State: IssueState.Open,
            Labels: ["bug"],
            Assignees: ["alice"],
            Milestone: null,
            CreatedAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero),
            HtmlUrl: "https://github.com/owner/repo/issues/42");

        var b = a with { };

        Assert.Equal(a, b);
    }

    [Fact]
    public void AgeDays_computes_from_CreatedAt_to_now()
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        var issue = new IssueView(
            Repo: "x/y", Number: 1, Title: "t", State: IssueState.Open,
            Labels: [], Assignees: [], Milestone: null,
            CreatedAt: now.AddDays(-7), UpdatedAt: now, HtmlUrl: "");

        Assert.Equal(7, issue.AgeDays(now));
    }

    [Fact]
    public void HasLabel_is_case_insensitive()
    {
        var issue = new IssueView(
            Repo: "x/y", Number: 1, Title: "t", State: IssueState.Open,
            Labels: ["Bug"], Assignees: [], Milestone: null,
            CreatedAt: DateTimeOffset.UtcNow, UpdatedAt: DateTimeOffset.UtcNow, HtmlUrl: "");

        Assert.True(issue.HasLabel("bug"));
        Assert.True(issue.HasLabel("BUG"));
        Assert.False(issue.HasLabel("feature"));
    }
}
