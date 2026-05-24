using GHKanban.AgentImage.Config;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class SkillConfigLoaderTests
{
    [Fact]
    public void Parses_full_agent_yaml_with_container_block()
    {
        var yaml = """
            name: Summariser
            implementation: container
            container:
              image: ghcr.io/x/agent:0.1
              llm:
                provider: anthropic
                model: claude-sonnet-4-6
                api-key-env: ANTHROPIC_API_KEY
              prompt:
                system: ./files/system.md
                user: Summarise {{issue.title}}
              tools:
                - github.post-comment
              timeout: 60s
            """;
        var cfg = SkillConfigLoader.Load(yaml);
        Assert.Equal("anthropic", cfg.Llm.Provider);
        Assert.Equal("./files/system.md", cfg.Prompt.SystemFile);
        Assert.Equal("Summarise {{issue.title}}", cfg.Prompt.User);
        Assert.Contains("github.post-comment", cfg.Tools);
    }

    [Fact]
    public void Defaults_provider_to_none_when_omitted()
    {
        var yaml = """
            name: x
            implementation: container
            container:
              image: i
              prompt:
                user: hi
            """;
        var cfg = SkillConfigLoader.Load(yaml);
        Assert.Equal("none", cfg.Llm.Provider);
    }
}
