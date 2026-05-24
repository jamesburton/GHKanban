using GHKanban.Config;
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Config.Tests;

public class YamlConfigLoaderTests
{
    [Fact]
    public void LoadsGitHubConfig()
    {
        var yaml = """
            auth:
              pat-env: MY_PAT
            webhook:
              public-url: https://example.test/hook
              secret-env: MY_SECRET
            poll-interval: 5m
            reconcile-interval: 30m
            """;
        var cfg = YamlConfigLoader.LoadGitHubConfig(yaml);
        Assert.Equal("MY_PAT", cfg.Auth.PatEnv);
        Assert.Equal("https://example.test/hook", cfg.Webhook.PublicUrl);
        Assert.Equal("MY_SECRET", cfg.Webhook.SecretEnv);
        Assert.Equal(TimeSpan.FromMinutes(5), cfg.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), cfg.ReconcileInterval);
    }

    [Fact]
    public void LoadsBoardConfig()
    {
        var yaml = """
            name: My Board
            scope:
              repos: [owner/foo, owner/bar]
              orgs: []
              filters:
                state: open
            columns:
              - name: Inbox
                rule: not has-label("triage")
              - name: Triage
                rule: has-label("triage")
            """;
        var cfg = YamlConfigLoader.LoadBoardConfig("my-board", yaml);
        Assert.Equal("my-board", cfg.Id);
        Assert.Equal("My Board", cfg.Name);
        Assert.Equal(2, cfg.Scope.Repos.Count);
        Assert.Equal(2, cfg.Columns.Count);
        Assert.Equal("Inbox", cfg.Columns[0].Name);
    }

    [Fact]
    public void LoadsAgentConfig()
    {
        var yaml = """
            name: Stub Acknowledger
            implementation: GHKanban.Agents.StubAcknowledgeAgent
            triggers:
              - on: issue.labeled
                when: label == "ai-pls"
            """;
        var cfg = YamlConfigLoader.LoadAgentConfig("stub-ack", yaml);
        Assert.Equal("Stub Acknowledger", cfg.Name);
        Assert.Single(cfg.Triggers);
        Assert.Equal("issue.labeled", cfg.Triggers[0].On);
    }

    [Fact]
    public void GitHubConfigDefaultsPollIntervalsWhenAbsent()
    {
        var yaml = """
            auth:
              pat-env: P
            webhook: {}
            """;
        var cfg = YamlConfigLoader.LoadGitHubConfig(yaml);
        Assert.Equal(TimeSpan.FromMinutes(5), cfg.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), cfg.ReconcileInterval);
    }

    [Fact]
    public void LoadsAgentConfigWithContainerSection()
    {
        var yaml = """
            name: Summariser
            implementation: container
            triggers:
              - on: issue.opened
                when: not has-label("nosummary")
            container:
              image: ghcr.io/jamesburton/ghkanban-agent:0.2.0
              llm:
                provider: anthropic
                model: claude-sonnet-4-6
                api-key-env: ANTHROPIC_API_KEY
              prompt:
                system: ./files/system.md
                user: |
                  Summarise: {{issue.title}}
              tools:
                - github.post-comment
              timeout: 60s
              resources:
                cpu: 1
                memory: 512m
            """;
        var cfg = YamlConfigLoader.LoadAgentConfig("summariser", yaml);
        Assert.Equal("container", cfg.Implementation);
        Assert.NotNull(cfg.Container);
        Assert.Equal("ghcr.io/jamesburton/ghkanban-agent:0.2.0", cfg.Container!.Image);
        Assert.Equal("anthropic", cfg.Container.Llm.Provider);
        Assert.Equal("ANTHROPIC_API_KEY", cfg.Container.Llm.ApiKeyEnv);
        Assert.Equal("./files/system.md", cfg.Container.Prompt.SystemFile);
        Assert.Contains("Summarise:", cfg.Container.Prompt.User);
        Assert.Single(cfg.Container.Tools);
        Assert.Equal(TimeSpan.FromSeconds(60), cfg.Container.Timeout);
        Assert.Equal(1.0, cfg.Container.CpuLimit);
        Assert.Equal(512L * 1024 * 1024, cfg.Container.MemoryBytes);
    }

    [Fact]
    public void LoadsAgentConfigWithoutContainerSection()
    {
        var yaml = """
            name: Stub
            implementation: stub
            triggers:
              - on: issue.labeled
                when: has-label("x")
            """;
        var cfg = YamlConfigLoader.LoadAgentConfig("stub", yaml);
        Assert.Equal("stub", cfg.Implementation);
        Assert.Null(cfg.Container);
    }
}
