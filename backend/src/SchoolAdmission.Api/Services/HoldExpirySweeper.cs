using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Services;

/// <summary>
/// Periodically rewrites HELD holds whose time ran out to EXPIRED. This is housekeeping only:
/// every query already treats a HELD hold past its expiry as expired, so seats are released on
/// time even if this job is slow, stopped, or running in several processes at once.
/// </summary>
public sealed class HoldExpirySweeper(
    IServiceScopeFactory scopeFactory,
    AppOptions options,
    ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SweepIntervalSeconds));
        do
        {
            await SweepOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

            var expired = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE holds SET status = 'EXPIRED', expired_at = clock_timestamp()
                WHERE status = 'HELD' AND expires_at <= clock_timestamp()
                """, ct);

            if (expired > 0)
            {
                logger.LogInformation("Marked {Count} expired hold(s)", expired);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            // A database hiccup must not stop the loop; the next tick tries again.
            logger.LogWarning(ex, "Expiry sweep failed; will retry");
        }
    }
}
