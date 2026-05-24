namespace GHKanban.AgentImage.Tools;

public interface IAgentTool
{
    string Name { get; }
    Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default);
}
