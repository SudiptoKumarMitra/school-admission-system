using Npgsql;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public class StartupMigrationTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task Several_processes_starting_together_migrate_one_database_without_conflict()
    {
        // The fixture creates an empty database: nothing is migrated yet.
        var factories = Enumerable.Range(0, 4).Select(_ => new ApiFactory(database.ConnectionString)).ToList();
        try
        {
            var clients = await Task.WhenAll(factories.Select(f => Task.Run(() => f.CreateClient())));

            foreach (var client in clients)
            {
                var response = await client.GetAsync("/health");
                Assert.True(response.IsSuccessStatusCode);
            }

            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\"", connection);
            Assert.Equal(1L, await command.ExecuteScalarAsync());
        }
        finally
        {
            foreach (var factory in factories)
            {
                await factory.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Restarting_against_an_already_migrated_database_succeeds()
    {
        await using (var first = new ApiFactory(database.ConnectionString))
        {
            first.CreateClient();
        }

        await using var second = new ApiFactory(database.ConnectionString);
        var response = await second.CreateClient().GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }
}
