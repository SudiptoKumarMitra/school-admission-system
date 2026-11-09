namespace SchoolAdmission.Api.Http;

/// <summary>Factory for the API's well-known errors (see 03-api-contract.md).</summary>
public static class ApiErrors
{
    public static ApiError StudentIdRequired() => new(
        StatusCodes.Status400BadRequest, ErrorCodes.StudentIdRequired, "Student ID required",
        "The X-Student-Id header (17-digit birth registration number) is required.");

    public static ApiError InvalidStudentId() => new(
        StatusCodes.Status400BadRequest, ErrorCodes.InvalidStudentId, "Invalid student ID",
        "X-Student-Id must be exactly 17 digits.");

    public static ApiError ClassNotFound() =>
        ProblemResults.NotFound(ErrorCodes.ClassNotFound, "The class was not found.");

    public static ApiError HoldNotFound() =>
        ProblemResults.NotFound(ErrorCodes.HoldNotFound, "The application was not found.");

    public static ApiError NotHoldOwner() => new(
        StatusCodes.Status403Forbidden, ErrorCodes.NotHoldOwner, "Not your application",
        "This application belongs to another student.");

    public static ApiError NoSeatsAvailable(string className) => ProblemResults.Conflict(
        ErrorCodes.NoSeatsAvailable, "No seats available", $"Class '{className}' has no available seats.");

    public static ApiError ActiveHoldExists() => ProblemResults.Conflict(
        ErrorCodes.ActiveHoldExists, "Application already exists",
        "You already have an active application for this class.");

    public static ApiError AlreadyConfirmed() => ProblemResults.Conflict(
        ErrorCodes.AlreadyConfirmed, "Already confirmed",
        "Your admission to this class is already confirmed.");

    public static ApiError HoldExpired() => ProblemResults.Conflict(
        ErrorCodes.HoldExpired, "Application expired", "The application has expired.");

    public static ApiError HoldCancelled() => ProblemResults.Conflict(
        ErrorCodes.HoldCancelled, "Application cancelled", "The application was cancelled.");

    public static ApiError CannotCancelConfirmed() => ProblemResults.Conflict(
        ErrorCodes.CannotCancelConfirmed, "Cannot cancel", "A confirmed admission cannot be cancelled.");
}
