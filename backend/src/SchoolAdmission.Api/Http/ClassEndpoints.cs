using System.Text.Json;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Http;

public static class ClassEndpoints
{
    public static IEndpointRouteBuilder MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/classes");

        group.MapPost("", CreateClass);
        group.MapGet("", ListClasses);
        group.MapGet("/{classId}", GetClass);
        group.MapPost("/{classId}/holds", CreateHold);

        return app;
    }

    private static async Task<IResult> CreateClass(HttpRequest request, ClassService classes, CancellationToken ct)
    {
        JsonElement body;
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            body = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ProblemResults.Validation("Request body must be valid JSON.").ToResult();
        }

        if (!CreateClassRequest.TryParse(body, out var parsed, out var errors))
        {
            return ProblemResults.Validation("Please correct the highlighted fields.", errors).ToResult();
        }

        var created = await classes.CreateAsync(parsed!, ct);
        return Results.Created($"/classes/{created.Id}", created);
    }

    private static async Task<IResult> ListClasses(ClassService classes, CancellationToken ct) =>
        Results.Ok(await classes.ListAsync(ct));

    private static async Task<IResult> GetClass(string classId, ClassService classes, CancellationToken ct)
    {
        if (!Ids.TryParseClass(classId, out var id))
        {
            return ClassNotFound().ToResult();
        }

        var found = await classes.GetAsync(id, ct);
        return found is null ? ClassNotFound().ToResult() : Results.Ok(found);
    }

    private static async Task<IResult> CreateHold(
        string classId, HttpRequest request, HoldService holds, CancellationToken ct)
    {
        // The header is validated first, so a bad header gets 400 even for an unknown class.
        var (studentId, headerError) = StudentIdHeader.Read(request);
        if (headerError is not null)
        {
            return headerError.ToResult();
        }

        if (!Ids.TryParseClass(classId, out var id))
        {
            return ApiErrors.ClassNotFound().ToResult();
        }

        var result = await holds.CreateHoldAsync(id, studentId!, ct);
        return result.IsSuccess
            ? Results.Created($"/holds/{result.Value!.HoldId}", result.Value)
            : result.Error!.ToResult();
    }

    private static ApiError ClassNotFound() => ApiErrors.ClassNotFound();
}
