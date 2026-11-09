namespace SchoolAdmission.Api.Configuration;

public static class AppOptionsLoader
{
    public const int DefaultHoldDurationSeconds = 600;
    public const int DefaultSweepIntervalSeconds = 5;

    /// <summary>Reads and validates settings. Throws with a clear message so startup fails fast.</summary>
    public static AppOptions Load(IConfiguration configuration)
    {
        var holdSeconds = ReadPositiveInt(configuration, "HOLD_DURATION_SECONDS", DefaultHoldDurationSeconds);
        var sweepSeconds = ReadPositiveInt(configuration, "EXPIRY_SWEEP_INTERVAL_SECONDS", DefaultSweepIntervalSeconds);

        var originsRaw = configuration["CORS_ALLOWED_ORIGINS"];
        var origins = string.IsNullOrWhiteSpace(originsRaw)
            ? new[] { "*" }
            : originsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new AppOptions(holdSeconds, sweepSeconds, origins);
    }

    private static int ReadPositiveInt(IConfiguration configuration, string key, int defaultValue)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid {key} '{raw}': it must be a positive whole number of seconds (for example {defaultValue}).");
        }

        return value;
    }
}
