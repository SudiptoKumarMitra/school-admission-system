using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Http;

public static class StudentIdHeader
{
    /// <summary>Reads and validates X-Student-Id. Returns the ID, or the error to send back.</summary>
    public static (string? StudentId, ApiError? Error) Read(HttpRequest request)
    {
        var values = request.Headers[StudentId.HeaderName];
        if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
        {
            return (null, ApiErrors.StudentIdRequired());
        }

        if (values.Count > 1)
        {
            return (null, ApiErrors.InvalidStudentId());
        }

        var value = values[0]!.Trim();
        return StudentId.IsValid(value) ? (value, null) : (null, ApiErrors.InvalidStudentId());
    }
}
