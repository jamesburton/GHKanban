using GHKanban.ContainerRuntime;
using GHKanban.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

/// <summary>
/// Resolves <see cref="AgentConfig.Implementation"/> strings to instantiated
/// <see cref="IGHKanbanAgent"/> objects via the DI container.
/// </summary>
public sealed class AgentRegistry
{
    private readonly IServiceProvider _services;

    /// <summary>Initialises the registry with the application service provider.</summary>
    /// <param name="services">The DI service provider used to resolve agent dependencies.</param>
    public AgentRegistry(IServiceProvider services) { _services = services; }

    /// <summary>
    /// Resolves and instantiates the agent described by <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The agent configuration containing the fully-qualified implementation type name.</param>
    /// <returns>An instantiated <see cref="IGHKanbanAgent"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the implementation type cannot be found in any loaded assembly.
    /// </exception>
    public IGHKanbanAgent Resolve(AgentConfig config)
    {
        if (string.Equals(config.Implementation, "container", StringComparison.OrdinalIgnoreCase))
        {
            var runtime = _services.GetRequiredService<IContainerRuntime>();
            var dirs = _services.GetRequiredService<ContainerAgentDirs>();
            var loggerFactory = _services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ContainerAgent>();
            return new ContainerAgent(config.Name, config, runtime, dirs, logger);
        }

        // Existing in-process implementation lookup (Slice A path).
        var type = Type.GetType(config.Implementation)
                   ?? FindInLoadedAssemblies(config.Implementation)
                   ?? throw new InvalidOperationException($"Agent implementation not found: {config.Implementation}");

        var instance = ActivatorUtilities.CreateInstance(_services, type, config.Name);
        return (IGHKanbanAgent)instance;
    }

    private static Type? FindInLoadedAssemblies(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName))
            .FirstOrDefault(t => t is not null);
}
