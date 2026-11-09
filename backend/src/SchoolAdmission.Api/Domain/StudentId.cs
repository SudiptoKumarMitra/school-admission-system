namespace SchoolAdmission.Api.Domain;

/// <summary>The student identity: a 17-digit birth registration number sent in X-Student-Id.</summary>
public static class StudentId
{
    public const int Length = 17;
    public const string HeaderName = "X-Student-Id";

    public static bool IsValid(string? value) =>
        value is not null && value.Length == Length && value.All(c => c is >= '0' and <= '9');

    /// <summary>Hides all but the last four digits for log output.</summary>
    public static string Mask(string value) =>
        value.Length <= 4 ? "****" : new string('*', value.Length - 4) + value[^4..];
}
