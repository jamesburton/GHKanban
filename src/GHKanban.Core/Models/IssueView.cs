namespace GHKanban.Core.Models;

public enum IssueState { Open, Closed }

public sealed record IssueView(
    string Repo,
    int Number,
    string Title,
    IssueState State,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Assignees,
    string? Milestone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string HtmlUrl)
{
    public int AgeDays(DateTimeOffset now) => (int)(now - CreatedAt).TotalDays;

    public bool HasLabel(string label) =>
        Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));

    public bool HasAssignee(string user) =>
        Assignees.Any(a => string.Equals(a, user, StringComparison.OrdinalIgnoreCase));
}
