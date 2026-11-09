using Npgsql;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>Verifies the database itself enforces the rules the API relies on.</summary>
public class SchemaTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    private const string Student = "20152691234567890";

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertClassAsync(int seats = 10, string name = "Class 6 - Morning Shift")
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO classes (id, name, total_seats) VALUES (@id, @name, @seats)",
            ("id", id), ("name", name), ("seats", seats));
        return id;
    }

    private Task InsertHoldAsync(Guid classId, string studentId = Student, string status = "HELD") =>
        ExecuteAsync(
            "INSERT INTO holds (id, class_id, student_id, status, expires_at) " +
            "VALUES (@id, @c, @s, @st, now() + interval '10 minutes')",
            ("id", Guid.CreateVersion7()), ("c", classId), ("s", studentId), ("st", status));

    private static async Task AssertSqlStateAsync(string expected, Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(expected, ex.SqlState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    public async Task Class_with_seat_count_outside_range_is_rejected(int seats) =>
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => InsertClassAsync(seats));

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task Class_with_blank_name_is_rejected(string name) =>
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => InsertClassAsync(name: name));

    [Fact]
    public async Task Class_with_name_longer_than_100_characters_is_rejected() =>
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => InsertClassAsync(name: new string('x', 101)));

    [Fact]
    public async Task Duplicate_class_names_are_allowed()
    {
        await InsertClassAsync(name: "Class 6 - Morning Shift");
        await InsertClassAsync(name: "Class 6 - Morning Shift");
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("held")]
    public async Task Hold_with_invalid_status_is_rejected(string status)
    {
        var classId = await InsertClassAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => InsertHoldAsync(classId, status: status));
    }

    [Theory]
    [InlineData("1234567890123456")]
    [InlineData("1234567890123456a")]
    public async Task Hold_with_malformed_student_id_is_rejected(string studentId)
    {
        var classId = await InsertClassAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => InsertHoldAsync(classId, studentId));
    }

    [Fact]
    public async Task Hold_with_student_id_longer_than_17_characters_is_rejected()
    {
        var classId = await InsertClassAsync();

        // varchar(17) rejects it before the check constraint is evaluated.
        await AssertSqlStateAsync(
            PostgresErrorCodes.StringDataRightTruncation, () => InsertHoldAsync(classId, "123456789012345678"));
    }

    [Fact]
    public async Task Hold_for_unknown_class_is_rejected() =>
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, () => InsertHoldAsync(Guid.CreateVersion7()));

    [Theory]
    [InlineData("HELD")]
    [InlineData("CONFIRMED")]
    public async Task Student_cannot_have_two_active_or_confirmed_holds_in_one_class(string firstStatus)
    {
        var classId = await InsertClassAsync();
        await InsertHoldAsync(classId, status: firstStatus);

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => InsertHoldAsync(classId));
    }

    [Theory]
    [InlineData("EXPIRED")]
    [InlineData("CANCELLED")]
    public async Task Student_can_apply_again_after_expiry_or_cancellation(string finishedStatus)
    {
        var classId = await InsertClassAsync();
        await InsertHoldAsync(classId, status: finishedStatus);

        await InsertHoldAsync(classId, status: "HELD");
    }

    [Fact]
    public async Task Same_student_can_hold_seats_in_different_classes()
    {
        await InsertHoldAsync(await InsertClassAsync());
        await InsertHoldAsync(await InsertClassAsync());
    }
}
