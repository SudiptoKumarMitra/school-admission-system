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

/// <summary>
/// Response of GET /holds/{holdId}. Deliberately has no student ID: this endpoint has no identity
/// check, so it must not reveal who applied. Status is the effective status.
/// </summary>
public sealed record HoldResponse(
    string HoldId,
    string ClassId,
    string Status,
    DateTimeOffset ExpiresAt,
    string ClassName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset ServerTime);

/// <summary>One item of GET /me/holds.</summary>
public sealed record MyHoldItem(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset ServerTime);
