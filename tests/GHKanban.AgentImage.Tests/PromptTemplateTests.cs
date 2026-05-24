using GHKanban.AgentImage.Config;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class PromptTemplateTests
{
    [Fact]
    public void Interpolates_known_tokens()
    {
        var ctx = new Dictionary<string, string>
        {
            ["issue.title"] = "Login broken",
            ["issue.body"] = "Steps:",
            ["issue.labels"] = "bug, auth",
            ["issue.repo"] = "owner/repo",
            ["issue.number"] = "42",
            ["trigger.event"] = "issue.opened",
            ["trigger.rule"] = "not has-label(\"x\")",
            ["run.id"] = "abc123",
        };
        var rendered = PromptTemplate.Render(
            "Issue {{issue.repo}}#{{issue.number}} ({{trigger.event}}): {{issue.title}}", ctx);

        Assert.Equal("Issue owner/repo#42 (issue.opened): Login broken", rendered);
    }

    [Fact]
    public void Leaves_unknown_tokens_unreplaced()
    {
        var rendered = PromptTemplate.Render("Hello {{unknown.token}}", new Dictionary<string, string>());
        Assert.Equal("Hello {{unknown.token}}", rendered);
    }
}
