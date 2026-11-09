namespace SchoolAdmission.Api.Domain;

/// <summary>Stored and returned hold status values (see 03-api-contract: status model).</summary>
public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = [Held, Confirmed, Expired, Cancelled];
}
