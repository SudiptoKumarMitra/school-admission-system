namespace SchoolAdmission.Api.Http;

/// <summary>Machine-readable error codes returned in the "code" field of problem+json responses.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string StudentIdRequired = "STUDENT_ID_REQUIRED";
    public const string InvalidStudentId = "INVALID_STUDENT_ID";
    public const string NotHoldOwner = "NOT_HOLD_OWNER";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string NotFound = "NOT_FOUND";
    public const string NoSeatsAvailable = "NO_SEATS_AVAILABLE";
    public const string ActiveHoldExists = "ACTIVE_HOLD_EXISTS";
    public const string AlreadyConfirmed = "ALREADY_CONFIRMED";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string CannotCancelConfirmed = "CANNOT_CANCEL_CONFIRMED";
    public const string InternalError = "INTERNAL_ERROR";
}
