namespace GHKanban.Core.Models;

public sealed record AgentConfig(
    string Id,
    string Name,
    string Implementation,
    IReadOnlyList<TriggerSpec> Triggers,
    ContainerAgentSpec? Container = null);

public sealed record ContainerAgentSpec(
    string Image,
    ContainerLlmSpec Llm,
    ContainerPromptSpec Prompt,
    IReadOnlyList<string> Tools,
    TimeSpan Timeout,
    double CpuLimit,
    long MemoryBytes);

public sealed record ContainerLlmSpec(
    string Provider,        // none | anthropic | openai
    string? Model,
    string? ApiKeyEnv);

public sealed record ContainerPromptSpec(
    string? SystemFile,     // path relative to agents/<id>/
    string User);
