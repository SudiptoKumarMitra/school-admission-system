using SchoolAdmission.Api.Http;

namespace SchoolAdmission.Api.Services;

/// <summary>Outcome of a service call: a value, or a business error to send back to the client.</summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, ApiError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ApiError? Error { get; }
    public bool IsSuccess => Error is null;

    public static ServiceResult<T> Ok(T value) => new(value, null);

    public static ServiceResult<T> Fail(ApiError error) => new(default, error);
}
