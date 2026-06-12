using System;
using System.Threading.Tasks;
using MySqlConnector;

namespace ViciOne.Suite.DataPort;

public sealed class MariaDbConnectionTester : IDataPortConnectionTester<MySqlDataPortCommunication>
{
    public static async Task TestConnectionAsync(MySqlDataPortCommunication communication)
    {
        MySqlDataPortProperties properties = new(communication);
        MySqlConnectionStringBuilder builder = new()
        {
            Server = properties.Server,
            Port = properties.Port,
            Database = properties.Database,
        };
        if (properties.UserId is not null)
            builder.UserID = properties.UserId;
        if (properties.Password is not null)
            builder.Password = properties.Password;

        using var connection = new MySqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync();
            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            throw new DataPortConnectionFailedException("Failed to connect to MariaDB.", ex);
        }
    }
}
