using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Http;

public static class HoldEndpoints
{
    public static IEndpointRouteBuilder MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/holds/{holdId}", GetHold);
        app.MapGet("/me/holds", GetMyHolds);
        return app;
    }

    private static async Task<IResult> GetHold(string holdId, HoldService holds, CancellationToken ct)
    {
        if (!Ids.TryParseHold(holdId, out var id))
        {
            return ApiErrors.HoldNotFound().ToResult();
        }

        var hold = await holds.GetAsync(id, ct);
        return hold is null ? ApiErrors.HoldNotFound().ToResult() : Results.Ok(hold);
    }

    private static async Task<IResult> GetMyHolds(HttpRequest request, HoldService holds, CancellationToken ct)
    {
        var (studentId, headerError) = StudentIdHeader.Read(request);
        if (headerError is not null)
        {
            return headerError.ToResult();
        }

        return Results.Ok(await holds.ListForStudentAsync(studentId!, ct));
    }
}
