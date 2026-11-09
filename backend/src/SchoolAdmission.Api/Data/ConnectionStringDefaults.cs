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

    /// <summary>
    /// Applies <see cref="DefaultMaxPoolSize"/> unless the connection string sets its own, and turns off
    /// Kerberos (GSS) probing: this app uses password authentication, and the slim runtime image does not
    /// ship the Kerberos library, so the probe only produces an error line at startup.
    /// </summary>
    public static string Apply(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!builder.ShouldSerialize("Maximum Pool Size"))
        {
            builder.MaxPoolSize = DefaultMaxPoolSize;
        }

        if (!builder.ShouldSerialize("GSS Encryption Mode"))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }
}
