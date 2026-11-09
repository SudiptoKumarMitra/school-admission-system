using Microsoft.Extensions.Configuration;
using SchoolAdmission.Api.Configuration;

namespace SchoolAdmission.Api.Tests;

public class AppOptionsLoaderTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Defaults_are_applied_when_nothing_is_configured()
    {
        var options = AppOptionsLoader.Load(Config());

        Assert.Equal(600, options.HoldDurationSeconds);
        Assert.Equal(5, options.SweepIntervalSeconds);
        Assert.Equal(new[] { "*" }, options.CorsAllowedOrigins);
    }

    [Fact]
    public void Hold_duration_is_read_from_environment_style_key()
    {
        var options = AppOptionsLoader.Load(Config(("HOLD_DURATION_SECONDS", "45")));

        Assert.Equal(45, options.HoldDurationSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("10 minutes")]
    public void Invalid_hold_duration_fails_fast_with_a_clear_message(string raw)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AppOptionsLoader.Load(Config(("HOLD_DURATION_SECONDS", raw))));

        Assert.Contains("HOLD_DURATION_SECONDS", ex.Message);
    }

    [Fact]
    public void Blank_hold_duration_falls_back_to_default()
    {
        var options = AppOptionsLoader.Load(Config(("HOLD_DURATION_SECONDS", "  ")));

        Assert.Equal(600, options.HoldDurationSeconds);
    }

    [Fact]
    public void Cors_origins_are_split_and_trimmed()
    {
        var options = AppOptionsLoader.Load(
            Config(("CORS_ALLOWED_ORIGINS", "http://a.example , http://b.example")));

        Assert.Equal(new[] { "http://a.example", "http://b.example" }, options.CorsAllowedOrigins);
    }
}
