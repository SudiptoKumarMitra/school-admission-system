using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

/// <summary>Waits for PostgreSQL and applies migrations; safe when several processes start together.</summary>
public static class DatabaseStartup
{
    // Arbitrary constant shared by every process so they take turns migrating.
    private const long MigrationLockKey = 727274;

    public static async Task WaitAndMigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken,
        int maxAttempts = 30,
        TimeSpan? retryDelay = null)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");

        await WaitForDatabaseAsync(db, logger, maxAttempts, retryDelay ?? TimeSpan.FromSeconds(2), cancellationToken);

        // Keep one connection open so the session-level advisory lock covers the whole migration.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database is up to date.");
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public static async Task WaitForDatabaseAsync(
        AdmissionDbContext db,
        ILogger logger,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (await db.Database.CanConnectAsync(cancellationToken))
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Database connection attempt {Attempt} failed", attempt);
            }

            if (attempt >= maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Could not connect to the database after {maxAttempts} attempts. " +
                    "Check ConnectionStrings__Default and that PostgreSQL is running.");
            }

            logger.LogInformation("Waiting for the database (attempt {Attempt}/{Max})...", attempt, maxAttempts);
            await Task.Delay(retryDelay, cancellationToken);
        }
    }
}
