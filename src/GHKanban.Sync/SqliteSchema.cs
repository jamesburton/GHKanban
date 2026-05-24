using Microsoft.Data.Sqlite;

namespace GHKanban.Sync;

public static class SqliteSchema
{
    public static void Apply(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sync_cursor (
              repo TEXT PRIMARY KEY,
              cursor TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS webhook_events (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              event_id TEXT NOT NULL,
              received_at TEXT NOT NULL,
              payload TEXT NOT NULL,
              UNIQUE(event_id)
            );
            CREATE TABLE IF NOT EXISTS agent_runs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              agent_name TEXT NOT NULL,
              trigger_event TEXT NOT NULL,
              repo TEXT NOT NULL,
              issue_number INTEGER NOT NULL,
              started_at TEXT NOT NULL,
              finished_at TEXT NOT NULL,
              status TEXT NOT NULL,
              output TEXT,
              error TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_agent_runs_started ON agent_runs(started_at DESC);
            """;
        cmd.ExecuteNonQuery();
    }
}
