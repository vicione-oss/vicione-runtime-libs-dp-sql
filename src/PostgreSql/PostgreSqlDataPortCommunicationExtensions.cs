using Npgsql;

namespace ViciOne.Suite.DataPort;

internal static class PostgreSqlDataPortCommunicationExtensions
{
    internal static string ToConnectionString(this PostgreSqlDataPortCommunication communication)
    {
        PostgreSqlDataPortProperties properties = new(communication);
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = properties.Host,
            Port = properties.Port,
            Timeout = properties.ConnectTimeout,
        };
        if (properties.Database is not null)
            builder.Database = properties.Database;
        if (properties.UserId is not null)
            builder.Username = properties.UserId;
        if (properties.Password is not null)
            builder.Password = properties.Password;
        return builder.ToString();
    }
}
