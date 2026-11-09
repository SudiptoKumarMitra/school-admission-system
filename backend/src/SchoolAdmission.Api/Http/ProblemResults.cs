namespace SchoolAdmission.Api.Http;

/// <summary>A business or validation failure that maps to an RFC 7807 problem+json response.</summary>
public sealed record ApiError(
    int Status,
    string Code,
    string Title,
    string Detail,
    IDictionary<string, string[]>? Errors = null);

public static class ProblemResults
{
    public static IResult ToResult(this ApiError error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Errors is not null)
        {
            extensions["errors"] = error.Errors;
        }

        return Results.Problem(
            statusCode: error.Status,
            title: error.Title,
            detail: error.Detail,
            type: "about:blank",
            extensions: extensions);
    }

    public static ApiError Validation(string detail, IDictionary<string, string[]>? errors = null) =>
        new(StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "Invalid input", detail, errors);

    public static ApiError NotFound(string code, string detail) =>
        new(StatusCodes.Status404NotFound, code, "Not found", detail);

    public static ApiError Conflict(string code, string title, string detail) =>
        new(StatusCodes.Status409Conflict, code, title, detail);
}
