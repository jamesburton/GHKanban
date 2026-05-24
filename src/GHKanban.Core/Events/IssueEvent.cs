using GHKanban.Core.Models;

namespace GHKanban.Core.Events;

public sealed record IssueEvent(
    EventType Type,
    IssueView Issue,
    string? ChangedLabel,         // populated for IssueLabeled / IssueUnlabeled
    string? ChangedAssignee,      // populated for IssueAssigned / IssueUnassigned
    string? CommentBody,          // populated for IssueCommentCreated
    DateTimeOffset At);
