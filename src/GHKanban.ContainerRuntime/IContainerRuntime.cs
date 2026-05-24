namespace GHKanban.ContainerRuntime;

/// <summary>
/// Spawns ephemeral containers per invocation. Single-node only in B1; alternative
/// implementations (k3s, Nomad, remote Docker) plug in behind this interface in later slices.
/// </summary>
public interface IContainerRuntime
{
    /// <summary>
    /// Create + start + wait + collect logs + delete. Returns when the container exits or times out.
    /// Caller is responsible for ensuring host paths in <see cref="ContainerSpec.Mounts"/> exist.
    /// </summary>
    Task<ContainerRunResult> RunAsync(ContainerSpec spec, CancellationToken ct = default);

    /// <summary>
    /// Lists existing containers with the given label key=value. Used by ContainerJanitor.
    /// </summary>
    Task<IReadOnlyList<ContainerHandle>> ListLabeledAsync(string labelKey, string labelValue, CancellationToken ct = default);

    /// <summary>
    /// Forcibly removes a container by ID. Idempotent: missing containers are not an error.
    /// </summary>
    Task RemoveAsync(string id, CancellationToken ct = default);
}

public sealed record ContainerHandle(string Id, string State, DateTimeOffset FinishedAt);
