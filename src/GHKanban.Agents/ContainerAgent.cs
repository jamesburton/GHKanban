using System.Text.Json;
using GHKanban.ContainerRuntime;
using GHKanban.Core.Models;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

public sealed record ContainerAgentDirs(string ConfigRoot, string RunsRoot);

public sealed class ContainerAgent : IGHKanbanAgent
{
    private readonly AgentConfig _config;
    private readonly IContainerRuntime _runtime;
    private readonly ContainerAgentDirs _dirs;
    private readonly ILogger<ContainerAgent> _log;

    public string Name { get; }

    public ContainerAgent(string name, AgentConfig config, IContainerRuntime runtime, ContainerAgentDirs dirs, ILogger<ContainerAgent> log)
    {
        Name = name;
        _config = config;
        _runtime = runtime;
        _dirs = dirs;
        _log = log;
    }

    public async Task<AgentRunResult> TriggerAsync(IssueContext ctx, CancellationToken ct = default)
    {
        var container = _config.Container
            ?? throw new InvalidOperationException($"Agent {_config.Id} has no container config");

        var runId = Guid.NewGuid().ToString("N");
        var runDir = Path.Combine(_dirs.RunsRoot, runId);
        Directory.CreateDirectory(runDir);
        var eventPath = Path.Combine(runDir, "event.json");

        try
        {
            await File.WriteAllTextAsync(eventPath, JsonSerializer.Serialize(ctx), ct);

            var mounts = BuildMounts(eventPath, container);
            var env = BuildEnv(ctx, runId);
            var labels = new Dictionary<string, string>
            {
                ["ghkanban"] = "true",
                ["ghkanban.agent"] = _config.Id,
                ["ghkanban.run-id"] = runId,
            };

            var spec = new ContainerSpec(
                Image: container.Image,
                Mounts: mounts,
                Env: env,
                Labels: labels,
                Timeout: container.Timeout,
                CpuLimit: container.CpuLimit,
                MemoryBytes: container.MemoryBytes);

            var result = await _runtime.RunAsync(spec, ct);

            if (result.TimedOut)
                return new AgentRunResult(AgentRunStatus.Failed, null, $"container timed out after {container.Timeout}");

            if (result.ExitCode != 0)
                return new AgentRunResult(AgentRunStatus.Failed, TrimForLog(result.Stdout), result.Stderr ?? "non-zero exit");

            return new AgentRunResult(AgentRunStatus.Success, TrimForLog(result.Stdout), null);
        }
        finally
        {
            try { Directory.Delete(runDir, true); }
            catch (Exception ex) { _log.LogWarning(ex, "Failed to clean up run dir {RunDir}", runDir); }
        }
    }

    private IReadOnlyList<ContainerMount> BuildMounts(string eventPath, ContainerAgentSpec container)
    {
        var mounts = new List<ContainerMount>
        {
            new(Path.Combine(_dirs.ConfigRoot, "agents", $"{_config.Id}.yaml"), "/skill/agent.yaml", ReadOnly: true),
            new(eventPath, "/event.json", ReadOnly: true),
            new(Path.Combine(_dirs.ConfigRoot, "secrets", "github-pat"), "/secrets/github-pat", ReadOnly: true),
        };

        var skillFilesDir = Path.Combine(_dirs.ConfigRoot, "agents", _config.Id);
        if (Directory.Exists(skillFilesDir))
            mounts.Add(new(skillFilesDir, "/skill/files", ReadOnly: true));

        if (container.Llm.ApiKeyEnv is not null)
        {
            var keyPath = Path.Combine(_dirs.ConfigRoot, "secrets", container.Llm.ApiKeyEnv);
            if (File.Exists(keyPath))
                mounts.Add(new(keyPath, "/secrets/llm-api-key", ReadOnly: true));
        }

        return mounts;
    }

    private static IReadOnlyDictionary<string, string> BuildEnv(IssueContext ctx, string runId) => new Dictionary<string, string>
    {
        ["GHKANBAN_RUN_ID"] = runId,
        ["GHKANBAN_GH_REPO"] = ctx.Issue.Repo,
        ["GHKANBAN_GH_ISSUE"] = ctx.Issue.Number.ToString(),
        ["GHKANBAN_LOG_FORMAT"] = "json",
    };

    private static string? TrimForLog(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        const int max = 2000;
        return s.Length <= max ? s : s[..max] + " …(truncated)";
    }
}
