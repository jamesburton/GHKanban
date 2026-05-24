using GHKanban.Core.Models;

namespace GHKanban.Config;

/// <summary>
/// In-memory snapshot of all loaded config. Replaced atomically on each reload.
/// </summary>
public sealed record ConfigSnapshot(
    GitHubConfig GitHub,
    IReadOnlyList<BoardConfig> Boards,
    IReadOnlyList<AgentConfig> Agents,
    IReadOnlyList<string> Errors);

public sealed class ConfigStore
{
    private ConfigSnapshot _current;
    private readonly object _lock = new();

    public ConfigStore(ConfigSnapshot initial) { _current = initial; }

    public ConfigSnapshot Current { get { lock (_lock) return _current; } }

    public event Action<ConfigSnapshot>? OnChange;

    public void Set(ConfigSnapshot s)
    {
        lock (_lock) _current = s;
        OnChange?.Invoke(s);
    }
}
