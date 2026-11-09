using Npgsql;

namespace SchoolAdmission.Api.Data;

public static class ConnectionStringDefaults
{
    /// <summary>
    /// Npgsql's own default is 100 connections per process, which equals PostgreSQL's default
    /// max_connections: a burst of requests (or a second backend process) would fail with
    /// "too many clients". A smaller default makes surplus requests queue in the pool instead.
    /// </summary>
    public const int DefaultMaxPoolSize = 30;

    /// <summary>Applies <see cref="DefaultMaxPoolSize"/> unless the connection string sets its own.</summary>
    public static string Apply(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!builder.ShouldSerialize("Maximum Pool Size"))
        {
            builder.MaxPoolSize = DefaultMaxPoolSize;
        }

        return builder.ConnectionString;
    }
}
