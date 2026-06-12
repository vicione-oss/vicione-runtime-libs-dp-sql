using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ViciOne.Suite.DataPort;

public sealed class SqliteConnectionTester : IDataPortConnectionTester<SqliteDataPortCommunication>
{
    public static async Task TestConnectionAsync(SqliteDataPortCommunication communication)
    {
        using SqliteConnection connection = new(communication.ToConnectionString());

        try
        {
            await connection.OpenAsync();
            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            throw new DataPortConnectionFailedException("Failed to connect to Sqlite.", ex);
        }
    }
}
