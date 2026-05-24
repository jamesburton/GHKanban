using System.Text.Json;
using GHKanban.Core.Events;
using GHKanban.Core.Models;

namespace GHKanban.GitHub;

/// <summary>Maps raw GitHub webhook payloads to <see cref="IssueEvent"/> domain objects.</summary>
public static class EventMapper
{
    /// <summary>
    /// Parses a GitHub webhook payload and returns an <see cref="IssueEvent"/>, or
    /// <see langword="null"/> when the event/action combination is not recognised.
    /// </summary>
    /// <param name="eventName">The value of the <c>X-GitHub-Event</c> header (e.g. "issues").</param>
    /// <param name="jsonBody">The raw JSON webhook body.</param>
    public static IssueEvent? MapIssueEvent(string eventName, string jsonBody)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        var action = root.GetProperty("action").GetString();

        var type = (eventName, action) switch
        {
            ("issues", "opened") => EventType.IssueOpened,
            ("issues", "labeled") => EventType.IssueLabeled,
            ("issues", "unlabeled") => EventType.IssueUnlabeled,
            ("issues", "assigned") => EventType.IssueAssigned,
            ("issues", "unassigned") => EventType.IssueUnassigned,
            ("issues", "closed") => EventType.IssueClosed,
            ("issues", "reopened") => EventType.IssueReopened,
            ("issue_comment", "created") => EventType.IssueCommentCreated,
            _ => (EventType?)null
        };

        if (type is null) return null;

        var issueEl = root.GetProperty("issue");
        var repo = root.GetProperty("repository").GetProperty("full_name").GetString()!;

        var labels = issueEl.GetProperty("labels").EnumerateArray()
            .Select(l => l.GetProperty("name").GetString()!)
            .ToList();

        var assignees = issueEl.GetProperty("assignees").EnumerateArray()
            .Select(a => a.GetProperty("login").GetString() ?? a.GetProperty("name").GetString()!)
            .ToList();

        string? milestone = null;
        if (issueEl.TryGetProperty("milestone", out var ms) && ms.ValueKind == JsonValueKind.Object)
            milestone = ms.GetProperty("title").GetString();

        var issue = new IssueView(
            repo,
            issueEl.GetProperty("number").GetInt32(),
            issueEl.GetProperty("title").GetString()!,
            issueEl.GetProperty("state").GetString() == "closed" ? IssueState.Closed : IssueState.Open,
            labels,
            assignees,
            milestone,
            issueEl.GetProperty("created_at").GetDateTimeOffset(),
            issueEl.GetProperty("updated_at").GetDateTimeOffset(),
            issueEl.GetProperty("html_url").GetString()!);

        // Resolve optional fields using plain if-statements to satisfy the definite-assignment
        // analyser under Nullable=enable + TreatWarningsAsErrors=true.
        string? changedLabel = null;
        if (type is EventType.IssueLabeled or EventType.IssueUnlabeled
            && root.TryGetProperty("label", out var lbl))
        {
            changedLabel = lbl.GetProperty("name").GetString();
        }

        string? changedAssignee = null;
        if (type is EventType.IssueAssigned or EventType.IssueUnassigned
            && root.TryGetProperty("assignee", out var asg))
        {
            changedAssignee = asg.GetProperty("login").GetString();
        }

        string? commentBody = null;
        if (type == EventType.IssueCommentCreated
            && root.TryGetProperty("comment", out var c))
        {
            commentBody = c.GetProperty("body").GetString();
        }

        return new IssueEvent(type.Value, issue, changedLabel, changedAssignee, commentBody, DateTimeOffset.UtcNow);
    }
}
