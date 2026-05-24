namespace GHKanban.AgentImage.Tools;

public sealed class ToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _byName;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _byName = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<IAgentTool> GetAllowed(IEnumerable<string> allowlist)
    {
        foreach (var name in allowlist)
        {
            if (_byName.TryGetValue(name, out var tool)) yield return tool;
        }
    }
}
