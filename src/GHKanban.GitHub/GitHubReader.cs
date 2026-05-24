using GHKanban.Core.Models;
using Octokit;

namespace GHKanban.GitHub;

/// <summary>Octokit-backed implementation of <see cref="IGitHubReader"/>.</summary>
public sealed class GitHubReader : IGitHubReader
{
    private readonly IGitHubClient _client;

    /// <summary>Initialises a new instance authenticated with the given personal access token.</summary>
    public GitHubReader(string personalAccessToken)
    {
        var conn = new Connection(new ProductHeaderValue("GHKanban", "0.1"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
        _client = new GitHubClient(conn);
    }

    // Test ctor for injecting a fake/mock IGitHubClient
    internal GitHubReader(IGitHubClient client) { _client = client; }

    /// <inheritdoc/>
    public async Task<IssuePage> ListIssuesAsync(string repo, string? afterCursor, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        var options = new ApiOptions { PageSize = 100, PageCount = 1 };
        var req = new RepositoryIssueRequest { State = ItemStateFilter.All };
        var issues = await _client.Issue.GetAllForRepository(owner, name, req, options);

        var mapped = issues.Where(i => i.PullRequest is null).Select(i => Map(i, repo)).ToList();
        var next = mapped.Count > 0 ? mapped.Max(i => i.UpdatedAt).ToString("O") : null;
        return new IssuePage(mapped, next);
    }

    /// <inheritdoc/>
    public async Task<string> GetCurrentUserLoginAsync(CancellationToken ct = default)
    {
        var user = await _client.User.Current();
        return user.Login;
    }

    /* i.Repository may be null on Issue objects from GetAllForRepository (plan inline fix §B9),
       so repo is passed in rather than derived from the Issue. */
    private static IssueView Map(Issue i, string repo) => new(
        Repo: repo,
        Number: i.Number,
        Title: i.Title,
        State: i.State.Value == ItemState.Closed ? IssueState.Closed : IssueState.Open,
        Labels: i.Labels.Select(l => l.Name).ToList(),
        Assignees: i.Assignees.Select(a => a.Login).ToList(),
        Milestone: i.Milestone?.Title,
        CreatedAt: i.CreatedAt,
        UpdatedAt: i.UpdatedAt ?? i.CreatedAt,
        HtmlUrl: i.HtmlUrl);

    private static (string Owner, string Name) SplitRepo(string repo)
    {
        var parts = repo.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : throw new ArgumentException($"Bad repo: {repo}");
    }
}
