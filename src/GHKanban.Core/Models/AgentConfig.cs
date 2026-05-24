namespace GHKanban.Core.Models;

public sealed record AgentConfig(
    string Id,
    string Name,
    string Implementation,
    IReadOnlyList<TriggerSpec> Triggers);
