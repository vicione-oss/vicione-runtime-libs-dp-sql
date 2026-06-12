using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ViciOne.Suite.DataPort;

public sealed class SqlServerConnectionTester : IDataPortConnectionTester<SqlServerDataPortCommunication>
{
    public static async Task TestConnectionAsync(SqlServerDataPortCommunication communication)
    {
        using SqlConnection connection = new(communication.ToConnectionString());

        try
        {
            await connection.OpenAsync();
            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            throw new DataPortConnectionFailedException("Failed to connect to SqlServer.", ex);
        }
    }
}
