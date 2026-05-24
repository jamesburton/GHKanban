using GHKanban.AgentImage.Config;
using GHKanban.AgentImage.Providers;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class ChatClientFactoryTests
{
    [Fact]
    public void Returns_null_for_none_provider()
    {
        var llm = new SkillLlm("none", null);
        Assert.Null(ChatClientFactory.Create(llm, apiKey: ""));
    }

    [Fact]
    public void Throws_for_unknown_provider()
    {
        var llm = new SkillLlm("bogus", "model-x");
        Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(llm, "key"));
    }

    [Fact]
    public void Creates_anthropic_client_for_anthropic_provider()
    {
        // We're not making a network call; just checking construction succeeds.
        var llm = new SkillLlm("anthropic", "claude-sonnet-4-6");
        using var client = ChatClientFactory.Create(llm, apiKey: "sk-ant-test");
        Assert.NotNull(client);
    }
}
