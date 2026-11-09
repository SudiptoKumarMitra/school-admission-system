namespace SchoolAdmission.Api.Configuration;

/// <summary>Validated application settings read from environment variables.</summary>
public sealed record AppOptions(
    int HoldDurationSeconds,
    int SweepIntervalSeconds,
    string[] CorsAllowedOrigins);
