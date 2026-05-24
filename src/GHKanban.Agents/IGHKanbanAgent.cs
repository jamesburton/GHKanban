using GHKanban.Core.Models;

namespace GHKanban.Agents;

/// <summary>
/// Wraps a Microsoft Agent Framework agent with a trigger entrypoint scoped to issue events.
/// Implementations encapsulate the agent logic; the runtime invokes TriggerAsync when a registered
/// trigger fires.
/// </summary>
public interface IGHKanbanAgent
{
    /// <summary>Gets the unique name of this agent.</summary>
    string Name { get; }

    /// <summary>Invoked by the agent runtime when a registered trigger fires for an issue.</summary>
    /// <param name="context">The issue context carrying the triggering event details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the agent run.</returns>
    Task<AgentRunResult> TriggerAsync(IssueContext context, CancellationToken ct = default);
}
