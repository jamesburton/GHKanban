namespace GHKanban.Web;

/// <summary>
/// Creates the default configuration directory structure on first run if it does not already exist.
/// </summary>
public static class FirstRunWizard
{
    /// <summary>
    /// Ensures the config root and default YAML files exist.
    /// No-ops if <paramref name="configRoot"/> already exists.
    /// </summary>
    /// <param name="configRoot">Absolute path to the GHKanban configuration directory.</param>
    public static void EnsureInitialised(string configRoot)
    {
        if (Directory.Exists(configRoot)) return;
        Directory.CreateDirectory(configRoot);
        Directory.CreateDirectory(Path.Combine(configRoot, "boards"));
        Directory.CreateDirectory(Path.Combine(configRoot, "agents"));
        Directory.CreateDirectory(Path.Combine(configRoot, "secrets"));

        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(Path.Combine(configRoot, "secrets"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch { /* best-effort */ }
        }

        File.WriteAllText(Path.Combine(configRoot, "github.yaml"), """
            # GHKanban GitHub configuration
            auth:
              # Name of an environment variable holding your GitHub Personal Access Token.
              # Required scopes: repo (and read:org for org-wide reads).
              pat-env: GHKANBAN_PAT
            webhook:
              # Optional. If set, the app will not auto-register webhooks on GitHub;
              # you must configure GH to POST to this URL. Use Tailscale Funnel / Cloudflare Tunnel / etc.
              # public-url: https://your-tailnet.ts.net/hook
              # secret-env: GHKANBAN_WEBHOOK_SECRET
            poll-interval: 5m
            reconcile-interval: 30m
            """);

        File.WriteAllText(Path.Combine(configRoot, "boards", "example.yaml"), """
            # Example board. Replace "your-org/your-repo" with a real repo.
            name: Example Board
            scope:
              repos: [your-org/your-repo]
            columns:
              - name: Inbox
                rule: not has-label("triage") and not has-label("in-progress")
              - name: Triage
                rule: has-label("triage")
              - name: In Progress
                rule: has-label("in-progress")
              - name: Stale
                rule: state == "open" and age-days > 30
            """);

        File.WriteAllText(Path.Combine(configRoot, "agents", "stub-ack.yaml"), """
            # Stub acknowledger — comments on issues that get the configured trigger.
            name: Stub Acknowledger
            implementation: GHKanban.Agents.StubAcknowledgeAgent
            triggers:
              - on: issue.labeled
                when: has-label("ai-pls")
            """);

        Console.WriteLine($"GHKanban: created configuration at {configRoot}");
        Console.WriteLine($"GHKanban: set the env var GHKANBAN_PAT to your GitHub Personal Access Token, then visit http://localhost:5454");
    }
}
