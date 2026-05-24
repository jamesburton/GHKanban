using Microsoft.Data.Sqlite;

namespace GHKanban.Sync;

public sealed class SyncCursorStore
{
    private readonly SqliteConnection _conn;

    public SyncCursorStore(SqliteConnection conn) { _conn = conn; }

    public async Task<string?> GetAsync(string repo, CancellationToken ct = default)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT cursor FROM sync_cursor WHERE repo = @r";
        cmd.Parameters.AddWithValue("@r", repo);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task SetAsync(string repo, string cursor, CancellationToken ct = default)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sync_cursor(repo, cursor, updated_at) VALUES (@r, @c, @u)
            ON CONFLICT(repo) DO UPDATE SET cursor = excluded.cursor, updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("@r", repo);
        cmd.Parameters.AddWithValue("@c", cursor);
        cmd.Parameters.AddWithValue("@u", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
