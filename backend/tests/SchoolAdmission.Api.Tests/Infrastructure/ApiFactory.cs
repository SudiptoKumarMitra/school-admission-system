using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>An in-process API host wired to a specific database and hold duration.</summary>
public sealed class ApiFactory(string connectionString, int holdSeconds = 600, int sweepSeconds = 1)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString());
        builder.UseSetting("EXPIRY_SWEEP_INTERVAL_SECONDS", sweepSeconds.ToString());
    }
}
