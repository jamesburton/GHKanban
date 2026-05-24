namespace GHKanban.Core.Models;

public enum AgentRunStatus { Success, Failed }

public sealed record AgentRunResult(
    AgentRunStatus Status,
    string? Output,
    string? Error);
