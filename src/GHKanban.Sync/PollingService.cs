using GHKanban.Config;
using GHKanban.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

/// <summary>
/// Background service that periodically polls GitHub for new/updated issues and writes them to
/// <see cref="IssueModelStore"/>.
/// </summary>
public sealed class PollingService : BackgroundService
{
    private readonly IGitHubReader _reader;
    private readonly IssueModelStore _store;
    private readonly ConfigStore _cfg;
    private readonly ILogger<PollingService> _log;

    public PollingService(IGitHubReader reader, IssueModelStore store, ConfigStore cfg, ILogger<PollingService> log)
    { _reader = reader; _store = store; _cfg = cfg; _log = log; }

    /// <summary>Performs a single poll pass over all configured repos.</summary>
    public async Task PollOnceAsync(CancellationToken ct)
    {
        var snap = _cfg.Current;
        var repos = snap.Boards.SelectMany(b => b.Scope.Repos).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in repos)
        {
            try
            {
                var page = await _reader.ListIssuesAsync(repo, afterCursor: null, ct);
                foreach (var i in page.Issues) _store.Upsert(i);
                _log.LogInformation("Polled {Count} issues for {Repo}", page.Issues.Count, repo);
            }
            catch (Exception ex) { _log.LogError(ex, "Poll failed for {Repo}", repo); }
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOnceAsync(stoppingToken);
            try { await Task.Delay(_cfg.Current.GitHub.PollInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
