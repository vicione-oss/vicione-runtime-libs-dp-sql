using System;
using System.Threading.Tasks;
using Npgsql;

namespace ViciOne.Suite.DataPort;

public sealed class PostgreSqlConnectionTester : IDataPortConnectionTester<PostgreSqlDataPortCommunication>
{
    public static async Task TestConnectionAsync(PostgreSqlDataPortCommunication communication)
    {
        using var connection = new NpgsqlConnection(communication.ToConnectionString());

        try
        {
            await connection.OpenAsync();
            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            throw new DataPortConnectionFailedException("Failed to connect to PostgreSql.", ex);
        }
    }
}
