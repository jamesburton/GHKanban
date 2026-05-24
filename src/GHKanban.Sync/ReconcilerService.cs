using GHKanban.Config;
using GHKanban.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

/// <summary>
/// Background service that performs a full re-sync of all issues on a slower reconciliation
/// interval, correcting any state drift missed by polling or webhooks.
/// </summary>
public sealed class ReconcilerService : BackgroundService
{
    private readonly IGitHubReader _reader;
    private readonly IssueModelStore _store;
    private readonly ConfigStore _cfg;
    private readonly ILogger<ReconcilerService> _log;

    public ReconcilerService(IGitHubReader reader, IssueModelStore store, ConfigStore cfg, ILogger<ReconcilerService> log)
    { _reader = reader; _store = store; _cfg = cfg; _log = log; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(_cfg.Current.GitHub.ReconcileInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }

            var snap = _cfg.Current;
            var repos = snap.Boards.SelectMany(b => b.Scope.Repos).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var repo in repos)
            {
                try
                {
                    var page = await _reader.ListIssuesAsync(repo, afterCursor: null, stoppingToken);
                    foreach (var i in page.Issues) _store.Upsert(i);
                    _log.LogInformation("Reconciled {Count} issues for {Repo}", page.Issues.Count, repo);
                }
                catch (Exception ex) { _log.LogError(ex, "Reconcile failed for {Repo}", repo); }
            }
        }
    }
}
