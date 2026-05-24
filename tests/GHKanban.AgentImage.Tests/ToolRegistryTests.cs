using GHKanban.AgentImage.Tools;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Returns_only_allowed_tools()
    {
        var fakeA = new FakeTool("github.post-comment");
        var fakeB = new FakeTool("github.add-label");
        var registry = new ToolRegistry(new IAgentTool[] { fakeA, fakeB });

        var allowed = registry.GetAllowed(new[] { "github.post-comment" }).ToList();

        Assert.Single(allowed);
        Assert.Equal("github.post-comment", allowed[0].Name);
    }

    private sealed class FakeTool : IAgentTool
    {
        public string Name { get; }
        public FakeTool(string name) => Name = name;
        public Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default) => Task.FromResult("");
    }
}
