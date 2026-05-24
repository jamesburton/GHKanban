namespace GHKanban.AgentImage.Config;

/// <summary>Represents the resolved configuration for a skill loaded from agent YAML.</summary>
public sealed record SkillConfig(
    SkillLlm Llm,
    SkillPrompt Prompt,
    IReadOnlyList<string> Tools);

/// <summary>LLM settings extracted from the <c>container.llm</c> block.</summary>
public sealed record SkillLlm(string Provider, string? Model);

/// <summary>Prompt settings extracted from the <c>container.prompt</c> block.</summary>
public sealed record SkillPrompt(string? SystemFile, string User);
