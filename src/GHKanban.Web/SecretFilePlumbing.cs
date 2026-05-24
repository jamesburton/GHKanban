using GHKanban.Config;

namespace GHKanban.Web;

internal static class SecretFilePlumbing
{
    public static void WriteAll(string secretsDir, ConfigSnapshot snap)
    {
        Directory.CreateDirectory(secretsDir);
        ApplyDirectoryPermissions(secretsDir);

        // GitHub PAT (mandatory; written even if env var is empty so the mount exists)
        var pat = Environment.GetEnvironmentVariable(snap.GitHub.Auth.PatEnv) ?? "";
        var patPath = Path.Combine(secretsDir, "github-pat");
        File.WriteAllText(patPath, pat);
        ApplyFilePermissions(patPath);

        // Per-agent LLM API keys
        foreach (var agent in snap.Agents)
        {
            if (agent.Container?.Llm.ApiKeyEnv is { } envName)
            {
                var key = Environment.GetEnvironmentVariable(envName) ?? "";
                var keyPath = Path.Combine(secretsDir, envName);
                File.WriteAllText(keyPath, key);
                ApplyFilePermissions(keyPath);
            }
        }
    }

    private static void ApplyDirectoryPermissions(string dir)
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, default ACLs inherit from parent (user profile) — acceptable for v1.
            // Production-grade ACL hardening lives in a later slice.
            return;
        }
        try { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch { /* best-effort */ }
    }

    private static void ApplyFilePermissions(string file)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { /* best-effort */ }
    }
}
