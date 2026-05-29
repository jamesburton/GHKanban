using Xunit;

namespace GHKanban.GitHub.Tests;

/// <summary>
/// Regression tests for the first-run scenario where the PAT env var has not been set yet.
/// Constructing GitHubReader / GitHubWriter with an empty PAT must produce an anonymous
/// Octokit client rather than throwing — otherwise DI startup crashes before the wizard
/// can run. See the v0.2.1-alpha hotfix.
/// </summary>
public class GitHubClientAnonymousCtorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void GitHubReader_AllowsEmptyOrWhitespaceToken(string pat)
    {
        // Should not throw. The instance is unusable for authenticated calls,
        // but DI startup must succeed.
        var reader = new GitHubReader(pat);
        Assert.NotNull(reader);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void GitHubWriter_AllowsEmptyOrWhitespaceToken(string pat)
    {
        var writer = new GitHubWriter(pat);
        Assert.NotNull(writer);
    }

    [Fact]
    public void GitHubReader_AcceptsRealLookingToken()
    {
        // Smoke test: a syntactically-valid token still constructs fine.
        var reader = new GitHubReader("ghp_1234567890abcdefghijklmnopqrstuvwxyz");
        Assert.NotNull(reader);
    }

    [Fact]
    public void GitHubWriter_AcceptsRealLookingToken()
    {
        var writer = new GitHubWriter("ghp_1234567890abcdefghijklmnopqrstuvwxyz");
        Assert.NotNull(writer);
    }
}
