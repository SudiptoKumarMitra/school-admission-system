using Npgsql;

namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>Direct database access for arranging or inspecting state in tests.</summary>
public sealed class TestDb(string connectionString)
{
    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Inserts a hold row directly. <paramref name="expiresInSeconds"/> may be negative.</summary>
    public async Task<string> InsertHoldAsync(string classId, string studentId, string status, int expiresInSeconds = 600)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO holds (id, class_id, student_id, status, expires_at) " +
            "VALUES (@id, @c, @s, @st, now() + make_interval(secs => @secs))",
            ("id", id), ("c", Guid.ParseExact(classId[2..], "N")), ("s", studentId),
            ("st", status), ("secs", (double)expiresInSeconds));
        return "h_" + id.ToString("N");
    }

    public Task<long> CountHoldsAsync(string classId, params string[] statuses) =>
        ScalarAsync<long>(
            "SELECT count(*) FROM holds WHERE class_id = @c AND status = ANY(@st)",
            ("c", Guid.ParseExact(classId[2..], "N")), ("st", statuses));
}
