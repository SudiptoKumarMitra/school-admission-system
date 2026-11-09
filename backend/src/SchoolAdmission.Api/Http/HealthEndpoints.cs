using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Http;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (AdmissionDbContext db, CancellationToken cancellationToken) =>
        {
            var reachable = false;
            try
            {
                reachable = await db.Database.CanConnectAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // reported as unhealthy below
            }

            return reachable
                ? Results.Ok(new { status = "ok" })
                : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
        return app;
    }
}
