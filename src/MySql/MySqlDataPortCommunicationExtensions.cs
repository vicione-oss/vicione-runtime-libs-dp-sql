using MySqlConnector;

namespace ViciOne.Suite.DataPort;

internal static class MySqlDataPortCommunicationExtensions
{
    internal static string ToConnectionString(this MySqlDataPortCommunication communication)
    {
        MySqlDataPortProperties properties = new(communication);
        MySqlConnectionStringBuilder builder = new()
        {
            Server = properties.Server,
            Port = properties.Port,
            Database = properties.Database,
            ConnectionTimeout = (uint)properties.ConnectTimeout,
        };
        if (properties.UserId is not null)
            builder.UserID = properties.UserId;
        if (properties.Password is not null)
            builder.Password = properties.Password;
        return builder.ToString();
    }
}
