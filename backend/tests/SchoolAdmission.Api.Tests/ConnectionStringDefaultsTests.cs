using Npgsql;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Tests;

public class ConnectionStringDefaultsTests
{
    [Fact]
    public void A_modest_pool_size_is_applied_when_none_is_configured()
    {
        var result = new NpgsqlConnectionStringBuilder(
            ConnectionStringDefaults.Apply("Host=db;Database=x;Username=u;Password=p"));

        Assert.Equal(ConnectionStringDefaults.DefaultMaxPoolSize, result.MaxPoolSize);
        Assert.Equal("db", result.Host);
    }

    [Theory]
    [InlineData("Host=db;Maximum Pool Size=7")]
    [InlineData("Host=db;MaxPoolSize=7")]
    public void An_explicit_pool_size_is_respected(string connectionString)
    {
        var result = new NpgsqlConnectionStringBuilder(ConnectionStringDefaults.Apply(connectionString));

        Assert.Equal(7, result.MaxPoolSize);
    }
}
