namespace GHKanban.GitHub;

/// <summary>Write operations against GitHub issues.</summary>
public interface IGitHubWriter
{
    /// <summary>Posts a comment on an issue.</summary>
    Task PostCommentAsync(string repo, int issueNumber, string body, CancellationToken ct = default);

    /// <summary>Adds a label to an issue.</summary>
    Task AddLabelAsync(string repo, int issueNumber, string label, CancellationToken ct = default);

    /// <summary>Assigns a user to an issue.</summary>
    Task AssignAsync(string repo, int issueNumber, string user, CancellationToken ct = default);
}
