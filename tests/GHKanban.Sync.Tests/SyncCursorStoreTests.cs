using GHKanban.Sync;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GHKanban.Sync.Tests;

public class SyncCursorStoreTests
{
    [Fact]
    public async Task RoundTripsCursor()
    {
        var conn = OpenInMemory();
        SqliteSchema.Apply(conn);
        var store = new SyncCursorStore(conn);

        await store.SetAsync("owner/repo", "cursor-abc", TestContext.Current.CancellationToken);
        var got = await store.GetAsync("owner/repo", TestContext.Current.CancellationToken);

        Assert.Equal("cursor-abc", got);
    }

    [Fact]
    public async Task ReturnsNullForUnknownRepo()
    {
        var conn = OpenInMemory();
        SqliteSchema.Apply(conn);
        var store = new SyncCursorStore(conn);
        Assert.Null(await store.GetAsync("unknown/repo", TestContext.Current.CancellationToken));
    }

    private static SqliteConnection OpenInMemory()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }
}
