using GHKanban.Web;
using Xunit;

namespace GHKanban.Web.Tests;

public class FirstRunWizardTests
{
    [Fact]
    public void EnsureInitialised_creates_expected_files_on_first_run()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ghkanban-wizard-test-{Guid.NewGuid():N}");
        try
        {
            FirstRunWizard.EnsureInitialised(dir);

            Assert.True(File.Exists(Path.Combine(dir, "github.yaml")), "github.yaml missing");
            Assert.True(File.Exists(Path.Combine(dir, "boards", "example.yaml")), "boards/example.yaml missing");
            Assert.True(File.Exists(Path.Combine(dir, "agents", "stub-ack.yaml")), "agents/stub-ack.yaml missing");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnsureInitialised_is_idempotent_when_dir_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ghkanban-wizard-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            // Directory exists — wizard should be a no-op and not throw.
            FirstRunWizard.EnsureInitialised(dir);

            // Since the dir pre-existed, the wizard skips template creation entirely.
            Assert.False(File.Exists(Path.Combine(dir, "github.yaml")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
