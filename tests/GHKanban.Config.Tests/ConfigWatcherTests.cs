using GHKanban.Config;
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Config.Tests;

public class ConfigWatcherTests
{
    [Fact]
    public async Task LoadsInitialConfigFromDirectory()
    {
        await Task.CompletedTask;
        var dir = MakeTempConfigDir();
        try
        {
            var snap = ConfigWatcher.LoadOnce(dir);
            Assert.Empty(snap.Errors);
            Assert.Equal("MY_PAT", snap.GitHub.Auth.PatEnv);
            Assert.Single(snap.Boards);
            Assert.Single(snap.Agents);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EmitsDiagnosticsForInvalidYaml()
    {
        await Task.CompletedTask;
        var dir = MakeTempConfigDir();
        File.WriteAllText(Path.Combine(dir, "boards", "bad.yaml"), "name: Bad\nscope:\n  repos: [unterminated");
        try
        {
            var snap = ConfigWatcher.LoadOnce(dir);
            Assert.NotEmpty(snap.Errors);
            Assert.Contains("bad.yaml", snap.Errors[0]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string MakeTempConfigDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ghkanban-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "boards"));
        Directory.CreateDirectory(Path.Combine(dir, "agents"));
        File.WriteAllText(Path.Combine(dir, "github.yaml"), """
            auth:
              pat-env: MY_PAT
            webhook: {}
            """);
        File.WriteAllText(Path.Combine(dir, "boards", "example.yaml"), """
            name: Example
            scope:
              repos: [owner/repo]
            columns:
              - name: Inbox
                rule: state == "open"
            """);
        File.WriteAllText(Path.Combine(dir, "agents", "stub.yaml"), """
            name: Stub
            implementation: GHKanban.Agents.StubAcknowledgeAgent
            triggers:
              - on: issue.labeled
                when: has-label("ai-pls")
            """);
        return dir;
    }
}
