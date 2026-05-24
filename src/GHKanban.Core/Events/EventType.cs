namespace GHKanban.Core.Events;

public enum EventType
{
    IssueOpened,
    IssueLabeled,
    IssueUnlabeled,
    IssueAssigned,
    IssueUnassigned,
    IssueClosed,
    IssueReopened,
    IssueCommentCreated
}
