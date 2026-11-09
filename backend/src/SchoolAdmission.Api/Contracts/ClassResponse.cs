namespace SchoolAdmission.Api.Contracts;

/// <summary>Class with live seat accounting. Field names are fixed by the assignment contract.</summary>
public sealed record ClassResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats,
    DateTimeOffset CreatedAt);
