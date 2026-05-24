namespace GHKanban.Core.Models;

/// <summary>
/// What an agent receives when triggered. Carries the issue plus the triggering event details.
/// </summary>
public sealed record IssueContext(
    IssueView Issue,
    string TriggerEvent,
    string MatchingRule,
    string AgentName);
