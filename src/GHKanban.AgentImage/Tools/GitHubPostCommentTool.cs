using System.Text.Json;
using Octokit;

namespace GHKanban.AgentImage.Tools;

public sealed class GitHubPostCommentTool : IAgentTool
{
    private readonly IGitHubClient _client;
    private readonly string _repo;
    private readonly int _issueNumber;

    public string Name => "github.post-comment";

    public GitHubPostCommentTool(string personalAccessToken, string repo, int issueNumber)
    {
        var conn = new Connection(new ProductHeaderValue("GHKanban-Agent", "0.2"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
        _client = new GitHubClient(conn);
        _repo = repo;
        _issueNumber = issueNumber;
    }

    /// <summary>
    /// Posts a comment. Accepts either a JSON object {"body": "..."} (when called as a tool by the model)
    /// or a plain text body (when invoked directly by the entrypoint with the model's text output).
    /// Returns the created comment URL.
    /// </summary>
    public async Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default)
    {
        string body;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            body = doc.RootElement.TryGetProperty("body", out var b) ? b.GetString() ?? "" : argumentsJson;
        }
        catch (JsonException)
        {
            body = argumentsJson;
        }

        var parts = _repo.Split('/', 2);
        if (parts.Length != 2) throw new ArgumentException($"Bad repo: {_repo}");

        var comment = await _client.Issue.Comment.Create(parts[0], parts[1], _issueNumber, body);
        return comment.HtmlUrl;
    }
}
