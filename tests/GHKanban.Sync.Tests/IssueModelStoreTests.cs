using GHKanban.Core.Models;
using GHKanban.Sync;
using Xunit;

namespace GHKanban.Sync.Tests;

public class IssueModelStoreTests
{
    [Fact]
    public void StoresAndRetrievesIssues()
    {
        var store = new IssueModelStore();
        var i = MakeIssue(1);
        store.Upsert(i);

        var got = store.GetIssue("owner/repo", 1);

        Assert.Equal(i, got);
    }

    [Fact]
    public void ReturnsAllIssuesForRepos()
    {
        var store = new IssueModelStore();
        store.Upsert(MakeIssue(1, repo: "a/b"));
        store.Upsert(MakeIssue(2, repo: "a/b"));
        store.Upsert(MakeIssue(3, repo: "c/d"));

        var got = store.GetIssuesForRepos(["a/b"]).ToList();

        Assert.Equal(2, got.Count);
    }

    [Fact]
    public void RaisesChangeEventOnUpsert()
    {
        var store = new IssueModelStore();
        var fired = false;
        store.OnChange += () => fired = true;
        store.Upsert(MakeIssue(1));
        Assert.True(fired);
    }

    private static IssueView MakeIssue(int n, string repo = "owner/repo") => new(
        repo, n, "t", IssueState.Open, [], [], null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, $"https://github.com/{repo}/issues/{n}");
}
