using System;
using System.Threading.Tasks;
using MySqlConnector;

namespace ViciOne.Suite.DataPort;

public sealed class MariaDbConnectionTester : IDataPortConnectionTester<MySqlDataPortCommunication>
{
    public static async Task TestConnectionAsync(MySqlDataPortCommunication communication)
    {
        using var connection = new MySqlConnection(communication.ToConnectionString());

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
