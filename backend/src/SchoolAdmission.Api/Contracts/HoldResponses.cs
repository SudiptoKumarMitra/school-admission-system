namespace SchoolAdmission.Api.Contracts;

/// <summary>201 response of POST /classes/{classId}/holds.</summary>
public sealed record CreateHoldResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt,
    string ClassName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ServerTime);
