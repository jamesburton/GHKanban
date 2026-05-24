using GHKanban.Core.Events;
using GHKanban.Core.Models;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

/// <summary>
/// Routes an <see cref="IssueEvent"/> to every registered <see cref="IGHKanbanAgent"/> whose
/// <see cref="AgentConfig"/> triggers match, then records the outcome via <see cref="AgentRunStore"/>.
/// </summary>
public sealed class AgentDispatcher
{
    private readonly IReadOnlyDictionary<string, IGHKanbanAgent> _agents;
    private readonly AgentRunStore _runs;
    private readonly string _currentUser;
    private readonly ILogger<AgentDispatcher> _log;

    /// <summary>Initialises a new <see cref="AgentDispatcher"/>.</summary>
    /// <param name="agents">Map of agent ID (case-insensitive) to agent instance.</param>
    /// <param name="runs">Store used to persist run records.</param>
    /// <param name="currentUser">The authenticated GitHub user name forwarded to trigger evaluation.</param>
    /// <param name="log">Logger.</param>
    public AgentDispatcher(
        IReadOnlyDictionary<string, IGHKanbanAgent> agents,
        AgentRunStore runs,
        string currentUser,
        ILogger<AgentDispatcher> log)
    { _agents = agents; _runs = runs; _currentUser = currentUser; _log = log; }

    /// <summary>
    /// Evaluates each config's triggers against <paramref name="ev"/> and invokes matching agents.
    /// Each matched trigger is executed and its result recorded independently.
    /// </summary>
    /// <param name="ev">The issue event to dispatch.</param>
    /// <param name="configs">Agent configurations to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DispatchAsync(IssueEvent ev, IEnumerable<AgentConfig> configs, CancellationToken ct)
    {
        foreach (var cfg in configs)
        {
            if (!_agents.TryGetValue(cfg.Id, out var agent))
            {
                _log.LogWarning("Agent {Id} configured but not registered; skipping", cfg.Id);
                continue;
            }

            foreach (var trigger in cfg.Triggers)
            {
                if (!TriggerEvaluator.Matches(trigger, ev, _currentUser)) continue;

                var ctx = new IssueContext(ev.Issue, trigger.On, trigger.When, cfg.Name);
                var started = DateTimeOffset.UtcNow;
                AgentRunResult result;
                try
                {
                    result = await agent.TriggerAsync(ctx, ct);
                }
                catch (Exception ex)
                {
                    result = new AgentRunResult(AgentRunStatus.Failed, null, ex.Message);
                }

                var finished = DateTimeOffset.UtcNow;
                await _runs.RecordAsync(new AgentRunRecord(
                    cfg.Name, trigger.On, ev.Issue.Repo, ev.Issue.Number,
                    started, finished, result.Status, result.Output, result.Error), ct);
            }
        }
    }
}
