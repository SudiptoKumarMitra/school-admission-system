using Npgsql;

namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>
/// Creates a throwaway PostgreSQL database for a test class and drops it afterwards.
/// Needs TEST_DATABASE_URL: a connection string for the server (database name is ignored).
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private string _adminConnectionString = string.Empty;
    private string _databaseName = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                "TEST_DATABASE_URL is not set. Run the tests with: " +
                "docker compose -f docker-compose.test.yml run --rm --build backend-tests");
        }

        var builder = new NpgsqlConnectionStringBuilder(server) { Database = "postgres" };
        _adminConnectionString = builder.ConnectionString;
        _databaseName = "sa_test_" + Guid.NewGuid().ToString("N");

        await ExecuteAdminAsync($"CREATE DATABASE {_databaseName}");

        builder.Database = _databaseName;
        // Several hosts and test classes run in parallel against one server: keep each pool modest.
        builder.MaxPoolSize = 20;
        ConnectionString = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)");
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
