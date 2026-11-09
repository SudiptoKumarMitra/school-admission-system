using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Tests;

public class DatabaseStartupTests
{
    [Fact]
    public async Task Unreachable_database_fails_with_a_clear_message_after_the_configured_attempts()
    {
        // Port 1 on localhost refuses connections immediately.
        var options = new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1")
            .Options;
        await using var db = new AdmissionDbContext(options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseStartup.WaitForDatabaseAsync(
                db, NullLogger.Instance, maxAttempts: 2, TimeSpan.FromMilliseconds(50), CancellationToken.None));

        Assert.Contains("after 2 attempts", ex.Message);
        Assert.Contains("ConnectionStrings__Default", ex.Message);
    }
}
