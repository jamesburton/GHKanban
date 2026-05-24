using GHKanban.Core.Models;

namespace GHKanban.Config;

/// <summary>
/// Watches a config directory for YAML file changes and reloads the <see cref="ConfigStore"/> with debouncing.
/// Use <see cref="LoadOnce"/> for a one-shot load without watching.
/// </summary>
public sealed class ConfigWatcher : IDisposable
{
    private readonly string _root;
    private readonly ConfigStore _store;
    private readonly FileSystemWatcher _watcher;
    private DateTime _lastReload = DateTime.MinValue;
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Initialises a new <see cref="ConfigWatcher"/> that monitors <paramref name="root"/> and pushes
    /// reloaded snapshots into <paramref name="store"/> when YAML files change.
    /// </summary>
    /// <param name="root">Path to the config root directory.</param>
    /// <param name="store">The <see cref="ConfigStore"/> to update on each reload.</param>
    public ConfigWatcher(string root, ConfigStore store)
    {
        _root = root;
        _store = store;
        _watcher = new FileSystemWatcher(root, "*.yaml")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => DebouncedReload();
        _watcher.Created += (_, _) => DebouncedReload();
        _watcher.Deleted += (_, _) => DebouncedReload();
        _watcher.Renamed += (_, _) => DebouncedReload();
    }

    private void DebouncedReload()
    {
        var now = DateTime.UtcNow;
        if (now - _lastReload < _debounce) return;
        _lastReload = now;
        Task.Delay(_debounce).ContinueWith(_ => _store.Set(LoadOnce(_root)));
    }

    /// <summary>
    /// Loads all config from <paramref name="root"/> in a single pass and returns a
    /// <see cref="ConfigSnapshot"/>. Parse errors are collected into <see cref="ConfigSnapshot.Errors"/>
    /// rather than thrown.
    /// </summary>
    /// <param name="root">Path to the config root directory.</param>
    /// <returns>A <see cref="ConfigSnapshot"/> with loaded config and any accumulated errors.</returns>
    public static ConfigSnapshot LoadOnce(string root)
    {
        var errors = new List<string>();
        GitHubConfig? github = null;
        var boards = new List<BoardConfig>();
        var agents = new List<AgentConfig>();

        var ghPath = Path.Combine(root, "github.yaml");
        if (File.Exists(ghPath))
        {
            try { github = YamlConfigLoader.LoadGitHubConfig(File.ReadAllText(ghPath)); }
            catch (Exception ex) { errors.Add($"github.yaml: {ex.Message}"); }
        }
        else
        {
            errors.Add("github.yaml not found");
        }

        var boardsDir = Path.Combine(root, "boards");
        if (Directory.Exists(boardsDir))
        {
            foreach (var f in Directory.GetFiles(boardsDir, "*.yaml"))
            {
                try
                {
                    var id = Path.GetFileNameWithoutExtension(f);
                    boards.Add(YamlConfigLoader.LoadBoardConfig(id, File.ReadAllText(f)));
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(f)}: {ex.Message}"); }
            }
        }

        var agentsDir = Path.Combine(root, "agents");
        if (Directory.Exists(agentsDir))
        {
            foreach (var f in Directory.GetFiles(agentsDir, "*.yaml"))
            {
                try
                {
                    var id = Path.GetFileNameWithoutExtension(f);
                    agents.Add(YamlConfigLoader.LoadAgentConfig(id, File.ReadAllText(f)));
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(f)}: {ex.Message}"); }
            }
        }

        return new ConfigSnapshot(
            github ?? new GitHubConfig(new GitHubAuth("UNSET"), new GitHubWebhook(null, null), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)),
            boards, agents, errors);
    }

    /// <inheritdoc/>
    public void Dispose() => _watcher.Dispose();
}
