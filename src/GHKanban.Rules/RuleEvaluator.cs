using GHKanban.Core.Models;

namespace GHKanban.Rules;

/// <summary>Evaluates a parsed <see cref="RuleNode"/> tree against an <see cref="IssueView"/>.</summary>
public sealed class RuleEvaluator
{
    private readonly DateTimeOffset _now;
    private readonly string _currentUser;

    /// <summary>Initialises the evaluator with a reference timestamp and the current user's login.</summary>
    public RuleEvaluator(DateTimeOffset now, string currentUser)
    { _now = now; _currentUser = currentUser; }

    /// <summary>Returns <see langword="true"/> when <paramref name="issue"/> satisfies <paramref name="node"/>.</summary>
    public bool Evaluate(RuleNode node, IssueView issue) => node switch
    {
        HasLabelNode n => issue.HasLabel(n.Label),
        AssigneeEqualsNode n => issue.HasAssignee(n.Username),
        AssigneeOfMineNode => issue.HasAssignee(_currentUser),
        StateEqualsNode n => string.Equals(n.State, issue.State.ToString(), StringComparison.OrdinalIgnoreCase),
        AgeDaysGreaterNode n => issue.AgeDays(_now) > n.Days,
        AgeDaysLessNode n => issue.AgeDays(_now) < n.Days,
        MilestoneEqualsNode n => string.Equals(issue.Milestone, n.Milestone, StringComparison.OrdinalIgnoreCase),
        RepoEqualsNode n => string.Equals(issue.Repo, n.Repo, StringComparison.OrdinalIgnoreCase),
        AndNode n => Evaluate(n.Left, issue) && Evaluate(n.Right, issue),
        OrNode n => Evaluate(n.Left, issue) || Evaluate(n.Right, issue),
        NotNode n => !Evaluate(n.Inner, issue),
        _ => throw new InvalidOperationException($"Unknown rule node: {node.GetType().Name}")
    };
}
