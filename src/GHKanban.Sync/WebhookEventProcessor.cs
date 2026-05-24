using System.Threading.Channels;
using GHKanban.Core.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

/// <summary>
/// Background service that drains a <see cref="Channel{T}"/> of <see cref="IssueEvent"/>s produced
/// by the webhook endpoint and applies each event to <see cref="IssueModelStore"/>.
/// </summary>
public sealed class WebhookEventProcessor : BackgroundService
{
    private readonly Channel<IssueEvent> _channel;
    private readonly IssueModelStore _store;
    private readonly ILogger<WebhookEventProcessor> _log;

    public WebhookEventProcessor(IssueModelStore store, ILogger<WebhookEventProcessor> log)
    {
        _channel = Channel.CreateUnbounded<IssueEvent>();
        _store = store;
        _log = log;
    }

    /// <summary>Writer side exposed to the webhook endpoint for enqueuing events.</summary>
    public ChannelWriter<IssueEvent> Writer => _channel.Writer;

    /// <summary>Reader side (primarily for testing).</summary>
    public ChannelReader<IssueEvent> Reader => _channel.Reader;

    /// <summary>
    /// Optional hook invoked after the store is updated for each event drained from the channel.
    /// Set by composition root (Program.cs) to route events to the agent dispatcher. Kept as a
    /// settable delegate rather than a constructor dependency to avoid creating a Sync → Agents
    /// project reference cycle (Agents already depends on Sync for SQLite schema/stores).
    /// </summary>
    public Func<IssueEvent, CancellationToken, Task>? OnEvent { get; set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var ev in _channel.Reader.ReadAllAsync(ct))
        {
            _store.Upsert(ev.Issue);
            _log.LogInformation("Webhook event {Type} for {Repo}#{Number}", ev.Type, ev.Issue.Repo, ev.Issue.Number);
            if (OnEvent is not null)
            {
                try { await OnEvent(ev, ct); }
                catch (Exception ex) { _log.LogError(ex, "OnEvent handler failed for {Repo}#{Number}", ev.Issue.Repo, ev.Issue.Number); }
            }
        }
    }
}
