using System.Collections.Concurrent;
using GHKanban.Core.Models;

namespace GHKanban.Sync;

/// <summary>
/// Thread-safe in-memory cache of all known issues across configured repos.
/// Populated by the sync engine (polling + webhook). Read by the UI.
/// </summary>
public sealed class IssueModelStore
{
    private readonly ConcurrentDictionary<(string Repo, int Number), IssueView> _store = new();

    public event Action? OnChange;

    public void Upsert(IssueView issue)
    {
        _store[(issue.Repo, issue.Number)] = issue;
        OnChange?.Invoke();
    }

    public IssueView? GetIssue(string repo, int number)
        => _store.TryGetValue((repo, number), out var i) ? i : null;

    public IEnumerable<IssueView> GetIssuesForRepos(IEnumerable<string> repos)
    {
        var set = repos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _store.Values.Where(i => set.Contains(i.Repo));
    }

    public IEnumerable<IssueView> All() => _store.Values;
}
