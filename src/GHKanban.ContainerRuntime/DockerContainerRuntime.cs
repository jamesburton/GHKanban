using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace GHKanban.ContainerRuntime;

/// <summary>
/// <see cref="IContainerRuntime"/> implementation backed by the local Docker daemon via the Docker.DotNet REST client.
/// </summary>
public sealed class DockerContainerRuntime : IContainerRuntime, IDisposable
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerContainerRuntime> _log;

    /// <summary>Initialises a new instance, connecting to the socket returned by <see cref="DockerSocketLocator.Resolve"/>.</summary>
    public DockerContainerRuntime(ILogger<DockerContainerRuntime> log)
    {
        _client = new DockerClientConfiguration(DockerSocketLocator.Resolve()).CreateClient();
        _log = log;
    }

    /// <inheritdoc/>
    public async Task<ContainerRunResult> RunAsync(ContainerSpec spec, CancellationToken ct = default)
    {
        // Pull the image so subsequent CreateContainer never fails with "image not found".
        try
        {
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = spec.Image },
                authConfig: null,
                progress: new Progress<JSONMessage>(),
                cancellationToken: ct);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Some registries return 404 for already-cached images; safe to ignore.
        }

        // Map mounts to Docker bind strings (host:container[:ro|rw]).
        var binds = spec.Mounts
            .Select(m => $"{m.HostPath}:{m.ContainerPath}:{(m.ReadOnly ? "ro" : "rw")}")
            .ToList();

        var createResponse = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = spec.Image,
            Env = spec.Env.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            Labels = new Dictionary<string, string>(spec.Labels),
            AttachStdout = true,
            AttachStderr = true,
            HostConfig = new HostConfig
            {
                Binds = binds,
                AutoRemove = false, // We remove explicitly in the finally block so we can collect logs first.
                NanoCPUs = (long)(spec.CpuLimit * 1_000_000_000L),
                Memory = spec.MemoryBytes,
                NetworkMode = "bridge",
            },
        }, ct);

        var id = createResponse.ID;

        try
        {
            await _client.Containers.StartContainerAsync(id, new ContainerStartParameters(), ct);

            // Wait with a per-spec timeout linked to the caller's token.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(spec.Timeout);

            bool timedOut = false;
            ContainerWaitResponse? waitResp = null;
            try
            {
                waitResp = await _client.Containers.WaitContainerAsync(id, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout fired, not the caller's token.
                timedOut = true;
                try
                {
                    await _client.Containers.KillContainerAsync(
                        id, new ContainerKillParameters(), CancellationToken.None);
                }
                catch (Exception killEx)
                {
                    _log.LogWarning(killEx, "Kill failed for timed-out container {Id}", id);
                }
            }

            var (stdout, stderr) = await ReadAllLogsAsync(id, ct);

            return new ContainerRunResult(
                ExitCode: timedOut ? -1 : (int)(waitResp?.StatusCode ?? -1),
                Stdout: stdout,
                Stderr: string.IsNullOrEmpty(stderr) ? null : stderr,
                TimedOut: timedOut);
        }
        finally
        {
            // Force-remove the container regardless of outcome.
            try
            {
                await _client.Containers.RemoveContainerAsync(
                    id, new ContainerRemoveParameters { Force = true }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Container removal failed for {Id}", id);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerHandle>> ListLabeledAsync(
        string labelKey, string labelValue, CancellationToken ct = default)
    {
        var resp = await _client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [$"{labelKey}={labelValue}"] = true },
            },
        }, ct);

        // ContainerListResponse.Created is DateTime (UTC from Docker); ContainerHandle.FinishedAt is
        // DateTimeOffset.  B1 placeholder: using creation time — the janitor will use InspectContainer
        // for an accurate FinishedAt in a later slice.
        return resp
            .Select(c => new ContainerHandle(
                Id: c.ID,
                State: c.State,
                FinishedAt: new DateTimeOffset(DateTime.SpecifyKind(c.Created, DateTimeKind.Utc), TimeSpan.Zero)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _client.Containers.RemoveContainerAsync(
                id, new ContainerRemoveParameters { Force = true }, ct);
        }
        catch (DockerContainerNotFoundException)
        {
            // Idempotent: container already gone is not an error.
        }
    }

    /// <summary>
    /// Reads all stdout and stderr lines from the container log stream after the container has exited.
    /// </summary>
    private async Task<(string stdout, string stderr)> ReadAllLogsAsync(string id, CancellationToken ct)
    {
        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        // tty:false → Docker multiplexes stdout/stderr; ReadOutputAsync returns a tagged ReadResult.
        var stream = await _client.Containers.GetContainerLogsAsync(
            id,
            tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = false },
            ct);

        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
            if (read.EOF) break;

            var text = Encoding.UTF8.GetString(buffer, 0, read.Count);

            if (read.Target == MultiplexedStream.TargetStream.StandardError)
                stderrSb.Append(text);
            else
                stdoutSb.Append(text);
        }

        return (stdoutSb.ToString(), stderrSb.ToString());
    }

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();
}
