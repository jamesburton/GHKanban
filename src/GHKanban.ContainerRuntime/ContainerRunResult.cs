namespace GHKanban.ContainerRuntime;

public sealed record ContainerRunResult(
    int ExitCode,
    string Stdout,
    string? Stderr,
    bool TimedOut);
