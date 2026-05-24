using GHKanban.Core.Models;

namespace GHKanban.GitHub;

/// <summary>Page of issues returned by <see cref="IGitHubReader.ListIssuesAsync"/>.</summary>
public sealed record IssuePage(IReadOnlyList<IssueView> Issues, string? NextCursor);

/// <summary>Read-only access to GitHub issue data.</summary>
public interface IGitHubReader
{
    /// <summary>Lists issues for the given repository, optionally paginated via a cursor.</summary>
    Task<IssuePage> ListIssuesAsync(string repo, string? afterCursor, CancellationToken ct = default);

    /// <summary>Returns the authenticated user's login name.</summary>
    Task<string> GetCurrentUserLoginAsync(CancellationToken ct = default);
}
