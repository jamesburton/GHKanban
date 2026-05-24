namespace GHKanban.ContainerRuntime;

public sealed record ContainerMount(string HostPath, string ContainerPath, bool ReadOnly);

public sealed record ContainerSpec(
    string Image,
    IReadOnlyList<ContainerMount> Mounts,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyDictionary<string, string> Labels,
    TimeSpan Timeout,
    double CpuLimit,
    long MemoryBytes);
