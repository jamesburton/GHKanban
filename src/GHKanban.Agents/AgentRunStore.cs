using GHKanban.Core.Models;
using Microsoft.Data.Sqlite;

namespace GHKanban.Agents;

public sealed record AgentRunRecord(
    string AgentName, string TriggerEvent, string Repo, int IssueNumber,
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    AgentRunStatus Status, string? Output, string? Error);

public sealed class AgentRunStore
{
    private readonly SqliteConnection _conn;

    public AgentRunStore(SqliteConnection conn) { _conn = conn; }

    public async Task RecordAsync(AgentRunRecord r, CancellationToken ct = default)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO agent_runs(agent_name, trigger_event, repo, issue_number, started_at, finished_at, status, output, error)
            VALUES (@n, @t, @r, @i, @s, @f, @st, @o, @e);
            """;
        cmd.Parameters.AddWithValue("@n", r.AgentName);
        cmd.Parameters.AddWithValue("@t", r.TriggerEvent);
        cmd.Parameters.AddWithValue("@r", r.Repo);
        cmd.Parameters.AddWithValue("@i", r.IssueNumber);
        cmd.Parameters.AddWithValue("@s", r.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@f", r.FinishedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@st", r.Status.ToString());
        cmd.Parameters.AddWithValue("@o", (object?)r.Output ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@e", (object?)r.Error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AgentRunRecord>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var list = new List<AgentRunRecord>();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT agent_name, trigger_event, repo, issue_number, started_at, finished_at, status, output, error FROM agent_runs ORDER BY started_at DESC LIMIT @l";
        cmd.Parameters.AddWithValue("@l", limit);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new AgentRunRecord(
                r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                DateTimeOffset.Parse(r.GetString(4)), DateTimeOffset.Parse(r.GetString(5)),
                Enum.Parse<AgentRunStatus>(r.GetString(6)),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8)));
        }
        return list;
    }
}
