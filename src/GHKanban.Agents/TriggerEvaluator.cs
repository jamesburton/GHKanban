using GHKanban.Core.Events;
using GHKanban.Core.Models;
using GHKanban.Rules;

namespace GHKanban.Agents;

/// <summary>
/// Determines whether an <see cref="IssueEvent"/> satisfies a <see cref="TriggerSpec"/>.
/// Matches on both the event-type name and the optional rule expression in <see cref="TriggerSpec.When"/>.
/// </summary>
public static class TriggerEvaluator
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ev"/> matches <paramref name="trigger"/>.
    /// </summary>
    /// <param name="trigger">The trigger specification to evaluate against.</param>
    /// <param name="ev">The issue event to test.</param>
    /// <param name="currentUser">The authenticated GitHub user name, forwarded to <see cref="RuleEvaluator"/>.</param>
    public static bool Matches(TriggerSpec trigger, IssueEvent ev, string currentUser)
    {
        if (!EventNameMatches(trigger.On, ev.Type)) return false;

        // Null, whitespace, or the literal "true" are treated as always-match.
        if (string.IsNullOrWhiteSpace(trigger.When) || trigger.When.Trim() == "true") return true;

        try
        {
            var ast = RuleParser.Parse(trigger.When);
            return new RuleEvaluator(ev.At, currentUser).Evaluate(ast, ev.Issue);
        }
        catch
        {
            // Malformed rule expressions are treated as non-matching to avoid crashing the pipeline.
            return false;
        }
    }

    private static bool EventNameMatches(string spec, EventType type) => spec switch
    {
        "issue.opened"          => type == EventType.IssueOpened,
        "issue.labeled"         => type == EventType.IssueLabeled,
        "issue.unlabeled"       => type == EventType.IssueUnlabeled,
        "issue.assigned"        => type == EventType.IssueAssigned,
        "issue.unassigned"      => type == EventType.IssueUnassigned,
        "issue.closed"          => type == EventType.IssueClosed,
        "issue.reopened"        => type == EventType.IssueReopened,
        "issue.comment.created" => type == EventType.IssueCommentCreated,
        _                       => false
    };
}
