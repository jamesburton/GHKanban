using System.Threading.Channels;
using GHKanban.Config;
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using GHKanban.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

/// <summary>
/// Background service that periodically polls GitHub for new/updated issues, writes them to
/// <see cref="IssueModelStore"/>, and synthesises label/assignee/state delta <see cref="IssueEvent"/>s
/// so that agent triggers fire for polling-driven changes as well as webhooks (spec §6).
/// </summary>
public sealed class PollingService : BackgroundService
{
    private readonly IGitHubReader _reader;
    private readonly IssueModelStore _store;
    private readonly ConfigStore _cfg;
    private readonly ChannelWriter<IssueEvent> _events;
    private readonly ILogger<PollingService> _log;

    public PollingService(
        IGitHubReader reader,
        IssueModelStore store,
        ConfigStore cfg,
        WebhookEventProcessor eventProcessor,
        ILogger<PollingService> log)
    {
        _reader = reader;
        _store = store;
        _cfg = cfg;
        _events = eventProcessor.Writer;
        _log = log;
    }

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
                foreach (var fresh in page.Issues)
                {
                    var prior = _store.GetIssue(fresh.Repo, fresh.Number);
                    _store.Upsert(fresh);

                    if (prior is null) continue; // first-seen issues don't synthesise events in v1
                    await EmitDeltaEventsAsync(prior, fresh, ct);
                }
                _log.LogInformation("Polled {Count} issues for {Repo}", page.Issues.Count, repo);
            }
            catch (Exception ex) { _log.LogError(ex, "Poll failed for {Repo}", repo); }
        }
    }

    private async Task EmitDeltaEventsAsync(IssueView prior, IssueView fresh, CancellationToken ct)
    {
        foreach (var added in fresh.Labels.Except(prior.Labels, StringComparer.OrdinalIgnoreCase))
        {
            var ev = new IssueEvent(EventType.IssueLabeled, fresh, added, null, null, DateTimeOffset.UtcNow);
            await _events.WriteAsync(ev, ct);
        }
        foreach (var removed in prior.Labels.Except(fresh.Labels, StringComparer.OrdinalIgnoreCase))
        {
            var ev = new IssueEvent(EventType.IssueUnlabeled, fresh, removed, null, null, DateTimeOffset.UtcNow);
            await _events.WriteAsync(ev, ct);
        }
        foreach (var added in fresh.Assignees.Except(prior.Assignees, StringComparer.OrdinalIgnoreCase))
        {
            var ev = new IssueEvent(EventType.IssueAssigned, fresh, null, added, null, DateTimeOffset.UtcNow);
            await _events.WriteAsync(ev, ct);
        }
        if (prior.State == IssueState.Open && fresh.State == IssueState.Closed)
            await _events.WriteAsync(new IssueEvent(EventType.IssueClosed, fresh, null, null, null, DateTimeOffset.UtcNow), ct);
        if (prior.State == IssueState.Closed && fresh.State == IssueState.Open)
            await _events.WriteAsync(new IssueEvent(EventType.IssueReopened, fresh, null, null, null, DateTimeOffset.UtcNow), ct);
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
