using GHKanban.Core.Models;
using GHKanban.GitHub;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

/// <summary>
/// A stub agent that acknowledges a trigger by posting a comment on the issue.
/// Used for testing and as a no-op placeholder in agent configurations.
/// </summary>
public sealed class StubAcknowledgeAgent : IGHKanbanAgent
{
    private readonly IGitHubWriter _writer;
    private readonly ILogger<StubAcknowledgeAgent> _log;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Initialises a new <see cref="StubAcknowledgeAgent"/>.</summary>
    /// <param name="name">The display name of this agent instance.</param>
    /// <param name="writer">GitHub writer used to post the acknowledgement comment.</param>
    /// <param name="log">Logger.</param>
    public StubAcknowledgeAgent(string name, IGitHubWriter writer, ILogger<StubAcknowledgeAgent> log)
    { Name = name; _writer = writer; _log = log; }

    /// <inheritdoc />
    public async Task<AgentRunResult> TriggerAsync(IssueContext ctx, CancellationToken ct = default)
    {
        var body =
            $"🤖 GHKanban: agent **{Name}** triggered by `{ctx.TriggerEvent}` (rule: `{ctx.MatchingRule}`).\n" +
            $"This is a stub acknowledgement — no other action taken.";
        try
        {
            await _writer.PostCommentAsync(ctx.Issue.Repo, ctx.Issue.Number, body, ct);
            _log.LogInformation("Stub posted ack on {Repo}#{Num}", ctx.Issue.Repo, ctx.Issue.Number);
            return new AgentRunResult(AgentRunStatus.Success, body, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Stub failed to post on {Repo}#{Num}", ctx.Issue.Repo, ctx.Issue.Number);
            return new AgentRunResult(AgentRunStatus.Failed, null, ex.Message);
        }
    }
}
