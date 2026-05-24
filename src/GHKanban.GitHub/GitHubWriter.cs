using Octokit;

namespace GHKanban.GitHub;

/// <summary>Octokit-backed implementation of <see cref="IGitHubWriter"/>.</summary>
public sealed class GitHubWriter : IGitHubWriter
{
    private readonly IGitHubClient _client;

    /// <summary>Initialises a new instance authenticated with the given personal access token.</summary>
    public GitHubWriter(string personalAccessToken)
    {
        var conn = new Connection(new ProductHeaderValue("GHKanban", "0.1"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
        _client = new GitHubClient(conn);
    }

    // Test ctor for injecting a fake/mock IGitHubClient
    internal GitHubWriter(IGitHubClient client) { _client = client; }

    /// <inheritdoc/>
    public async Task PostCommentAsync(string repo, int issueNumber, string body, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        await _client.Issue.Comment.Create(owner, name, issueNumber, body);
    }

    /// <inheritdoc/>
    public async Task AddLabelAsync(string repo, int issueNumber, string label, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        await _client.Issue.Labels.AddToIssue(owner, name, issueNumber, [label]);
    }

    /// <inheritdoc/>
    public async Task AssignAsync(string repo, int issueNumber, string user, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        await _client.Issue.Assignee.AddAssignees(owner, name, issueNumber, new AssigneesUpdate([user]));
    }

    private static (string Owner, string Name) SplitRepo(string repo)
    {
        var parts = repo.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : throw new ArgumentException($"Bad repo: {repo}");
    }
}
