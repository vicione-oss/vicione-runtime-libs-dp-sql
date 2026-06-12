using System;
using System.Threading.Tasks;
using MySqlConnector;

namespace ViciOne.Suite.DataPort;

public sealed class MySqlConnectionTester : IDataPortConnectionTester<MySqlDataPortCommunication>
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
            throw new DataPortConnectionFailedException("Failed to connect to MySQL.", ex);
        }
    }
}
