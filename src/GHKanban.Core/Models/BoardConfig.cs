namespace GHKanban.Core.Models;

public sealed record BoardScope(
    IReadOnlyList<string> Repos,
    IReadOnlyList<string> Orgs,
    IReadOnlyDictionary<string, string> Filters);

public sealed record BoardConfig(
    string Id,
    string Name,
    BoardScope Scope,
    IReadOnlyList<ColumnConfig> Columns);
