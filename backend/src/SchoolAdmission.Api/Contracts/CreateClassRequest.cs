using System.Text.Json;

namespace SchoolAdmission.Api.Contracts;

/// <summary>Request body of POST /classes, parsed by hand so every bad input yields a clear 400.</summary>
public sealed record CreateClassRequest(string Name, int TotalSeats)
{
    public const int MaxNameLength = 100;
    public const int MinSeats = 1;
    public const int MaxSeats = 100000;

    public static bool TryParse(
        JsonElement body,
        out CreateClassRequest? request,
        out Dictionary<string, string[]> errors)
    {
        request = null;
        errors = new Dictionary<string, string[]>();

        if (body.ValueKind != JsonValueKind.Object)
        {
            errors["body"] = ["Request body must be a JSON object."];
            return false;
        }

        string? name = null;
        if (!body.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            errors["name"] = ["Name is required and must be text."];
        }
        else
        {
            name = nameElement.GetString()!.Trim();
            if (name.Length is 0 or > MaxNameLength)
            {
                errors["name"] = [$"Name must be between 1 and {MaxNameLength} characters."];
            }
        }

        var seats = 0;
        if (!body.TryGetProperty("totalSeats", out var seatsElement)
            || seatsElement.ValueKind != JsonValueKind.Number
            || !seatsElement.TryGetInt32(out seats))
        {
            errors["totalSeats"] = ["Total seats is required and must be a whole number."];
        }
        else if (seats is < MinSeats or > MaxSeats)
        {
            errors["totalSeats"] = [$"Total seats must be between {MinSeats} and {MaxSeats}."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        request = new CreateClassRequest(name!, seats);
        return true;
    }
}
