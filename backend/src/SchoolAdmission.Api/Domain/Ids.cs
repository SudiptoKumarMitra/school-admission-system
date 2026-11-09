using System.Globalization;

namespace SchoolAdmission.Api.Domain;

/// <summary>Public IDs: a type prefix plus a random (UUIDv7) value, e.g. "c_0193a5c2...".</summary>
public static class Ids
{
    private const string ClassPrefix = "c_";
    private const string HoldPrefix = "h_";
    private const int HexLength = 32;

    public static Guid NewId() => Guid.CreateVersion7();

    public static string FormatClass(Guid id) => ClassPrefix + id.ToString("N");

    public static string FormatHold(Guid id) => HoldPrefix + id.ToString("N");

    public static bool TryParseClass(string? value, out Guid id) => TryParse(value, ClassPrefix, out id);

    public static bool TryParseHold(string? value, out Guid id) => TryParse(value, HoldPrefix, out id);

    private static bool TryParse(string? value, string prefix, out Guid id)
    {
        id = default;
        if (value is null
            || value.Length != prefix.Length + HexLength
            || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParseExact(value.AsSpan(prefix.Length), "N", out id);
    }
}
