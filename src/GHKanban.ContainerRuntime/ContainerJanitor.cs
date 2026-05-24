using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.ContainerRuntime;

/// <summary>
/// Background service that periodically removes exited containers older than
/// <see cref="OrphanThreshold"/>. Containers are identified by the label
/// <c>ghkanban=true</c>.
/// </summary>
public sealed class ContainerJanitor : BackgroundService
{
    private readonly IContainerRuntime _runtime;
    private readonly ILogger<ContainerJanitor> _log;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OrphanThreshold = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Initializes a new instance of <see cref="ContainerJanitor"/>.
    /// </summary>
    /// <param name="runtime">The container runtime used to list and remove containers.</param>
    /// <param name="log">Logger for sweep activity and errors.</param>
    public ContainerJanitor(IContainerRuntime runtime, ILogger<ContainerJanitor> log)
    {
        _runtime = runtime;
        _log = log;
    }

    /// <summary>
    /// Performs a single sweep: removes exited containers whose finish time is
    /// older than <paramref name="threshold"/> relative to <paramref name="now"/>.
    /// </summary>
    /// <param name="runtime">The container runtime to query and remove from.</param>
    /// <param name="now">The reference timestamp used to compute container age.</param>
    /// <param name="threshold">Age threshold; containers older than this are removed.</param>
    /// <param name="log">Logger for sweep activity and warnings.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SweepOnceAsync(
        IContainerRuntime runtime,
        DateTimeOffset now,
        TimeSpan threshold,
        ILogger log,
        CancellationToken ct)
    {
        var handles = await runtime.ListLabeledAsync("ghkanban", "true", ct);

        foreach (var h in handles)
        {
            if (h.State != "exited") continue;
            if (now - h.FinishedAt < threshold) continue;

            try
            {
                await runtime.RemoveAsync(h.Id, ct);
                log.LogInformation(
                    "Janitor removed orphan container {Id} (state={State}, finished={FinishedAt})",
                    h.Id, h.State, h.FinishedAt);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Janitor failed to remove {Id}", h.Id);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                await SweepOnceAsync(_runtime, DateTimeOffset.UtcNow, OrphanThreshold, _log, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Janitor sweep failed");
            }
        }
    }
}
