using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GHKanban.AgentImage.Config;

/// <summary>Parses agent YAML into a <see cref="SkillConfig"/>.</summary>
public static class SkillConfigLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    // Private raw types that mirror the YAML structure before mapping to the public POCOs.
    private sealed class RawAgent { public RawContainer? Container { get; set; } }

    private sealed class RawContainer
    {
        public RawLlm? Llm { get; set; }
        public RawPrompt? Prompt { get; set; }
        public List<string>? Tools { get; set; }
    }

    private sealed class RawLlm
    {
        public string? Provider { get; set; }
        public string? Model { get; set; }
    }

    private sealed class RawPrompt
    {
        public string? System { get; set; }
        public string? User { get; set; }
    }

    /// <summary>Deserializes <paramref name="yaml"/> and returns the validated <see cref="SkillConfig"/>.</summary>
    /// <param name="yaml">Raw YAML text from an agent skill file.</param>
    /// <returns>The parsed and validated skill configuration.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the YAML is empty, the <c>container</c> section is missing, or
    /// <c>container.prompt.user</c> is absent.
    /// </exception>
    public static SkillConfig Load(string yaml)
    {
        var raw = _deserializer.Deserialize<RawAgent>(yaml)
            ?? throw new InvalidOperationException("empty");

        var c = raw.Container
            ?? throw new InvalidOperationException("container section required for agent image");

        return new SkillConfig(
            Llm: new SkillLlm(c.Llm?.Provider ?? "none", c.Llm?.Model),
            Prompt: new SkillPrompt(
                c.Prompt?.System,
                c.Prompt?.User ?? throw new InvalidOperationException("prompt.user required")),
            Tools: c.Tools ?? new List<string>());
    }
}
