using GHKanban.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GHKanban.Config;

public static class YamlConfigLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private sealed class RawGitHub
    {
        public RawAuth? Auth { get; set; }
        public RawWebhook? Webhook { get; set; }
        public string? PollInterval { get; set; }
        public string? ReconcileInterval { get; set; }
    }

    private sealed class RawAuth { public string? PatEnv { get; set; } }

    private sealed class RawWebhook { public string? PublicUrl { get; set; } public string? SecretEnv { get; set; } }

    public static GitHubConfig LoadGitHubConfig(string yaml)
    {
        var raw = _deserializer.Deserialize<RawGitHub>(yaml) ?? throw new InvalidOperationException("empty github.yaml");
        return new GitHubConfig(
            Auth: new GitHubAuth(raw.Auth?.PatEnv ?? throw new InvalidOperationException("auth.pat-env required")),
            Webhook: new GitHubWebhook(raw.Webhook?.PublicUrl, raw.Webhook?.SecretEnv),
            PollInterval: ParseDuration(raw.PollInterval) ?? TimeSpan.FromMinutes(5),
            ReconcileInterval: ParseDuration(raw.ReconcileInterval) ?? TimeSpan.FromMinutes(30));
    }

    private sealed class RawBoard
    {
        public string? Name { get; set; }
        public RawScope? Scope { get; set; }
        public List<RawColumn>? Columns { get; set; }
    }

    private sealed class RawScope
    {
        public List<string>? Repos { get; set; }
        public List<string>? Orgs { get; set; }
        public Dictionary<string, string>? Filters { get; set; }
    }

    private sealed class RawColumn { public string? Name { get; set; } public string? Rule { get; set; } }

    public static BoardConfig LoadBoardConfig(string id, string yaml)
    {
        var raw = _deserializer.Deserialize<RawBoard>(yaml) ?? throw new InvalidOperationException("empty board yaml");
        var scope = new BoardScope(
            Repos: raw.Scope?.Repos ?? new(),
            Orgs: raw.Scope?.Orgs ?? new(),
            Filters: raw.Scope?.Filters ?? new());
        var cols = (raw.Columns ?? new())
            .Select(c => new ColumnConfig(c.Name ?? throw new InvalidOperationException("column.name required"),
                                          c.Rule ?? throw new InvalidOperationException("column.rule required")))
            .ToList();
        return new BoardConfig(id, raw.Name ?? id, scope, cols);
    }

    private sealed class RawContainer
    {
        public string? Image { get; set; }
        public RawLlm? Llm { get; set; }
        public RawPrompt? Prompt { get; set; }
        public List<string>? Tools { get; set; }
        public string? Timeout { get; set; }
        public RawResources? Resources { get; set; }
    }

    private sealed class RawLlm { public string? Provider { get; set; } public string? Model { get; set; } public string? ApiKeyEnv { get; set; } }

    private sealed class RawPrompt { public string? System { get; set; } public string? User { get; set; } }

    private sealed class RawResources { public double? Cpu { get; set; } public string? Memory { get; set; } }

    private sealed class RawAgent
    {
        public string? Name { get; set; }
        public string? Implementation { get; set; }
        public List<RawTrigger>? Triggers { get; set; }
        public RawContainer? Container { get; set; }
    }

    private sealed class RawTrigger { public string? On { get; set; } public string? When { get; set; } }

    public static AgentConfig LoadAgentConfig(string id, string yaml)
    {
        var raw = _deserializer.Deserialize<RawAgent>(yaml) ?? throw new InvalidOperationException("empty agent yaml");
        var triggers = (raw.Triggers ?? new())
            .Select(t => new TriggerSpec(t.On ?? throw new InvalidOperationException("trigger.on required"),
                                         t.When ?? "true"))
            .ToList();

        ContainerAgentSpec? container = null;
        if (raw.Container is not null)
        {
            var c = raw.Container;
            container = new ContainerAgentSpec(
                Image: c.Image ?? throw new InvalidOperationException("container.image required"),
                Llm: new ContainerLlmSpec(
                    Provider: c.Llm?.Provider ?? "none",
                    Model: c.Llm?.Model,
                    ApiKeyEnv: c.Llm?.ApiKeyEnv),
                Prompt: new ContainerPromptSpec(
                    SystemFile: c.Prompt?.System,
                    User: c.Prompt?.User ?? throw new InvalidOperationException("container.prompt.user required")),
                Tools: c.Tools ?? new List<string>(),
                Timeout: ParseDuration(c.Timeout) ?? TimeSpan.FromSeconds(60),
                CpuLimit: c.Resources?.Cpu ?? 1.0,
                MemoryBytes: ParseMemory(c.Resources?.Memory) ?? 512L * 1024 * 1024);
        }

        return new AgentConfig(
            Id: id,
            Name: raw.Name ?? id,
            Implementation: raw.Implementation ?? "stub",
            Triggers: triggers,
            Container: container);
    }

    private static long? ParseMemory(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith("g")) return long.Parse(s[..^1]) * 1024L * 1024 * 1024;
        if (s.EndsWith("m")) return long.Parse(s[..^1]) * 1024L * 1024;
        if (s.EndsWith("k")) return long.Parse(s[..^1]) * 1024;
        return long.Parse(s);
    }

    private static TimeSpan? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.EndsWith("ms")) return TimeSpan.FromMilliseconds(int.Parse(s[..^2]));
        if (s.EndsWith("s")) return TimeSpan.FromSeconds(int.Parse(s[..^1]));
        if (s.EndsWith("m")) return TimeSpan.FromMinutes(int.Parse(s[..^1]));
        if (s.EndsWith("h")) return TimeSpan.FromHours(int.Parse(s[..^1]));
        throw new FormatException($"unrecognised duration: {s}");
    }
}
