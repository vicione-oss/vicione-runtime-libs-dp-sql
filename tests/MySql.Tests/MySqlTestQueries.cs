using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace ViciOne.Suite.DataPort;

[SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
internal static class MySqlTestQueries
{
    internal static async Task ExecuteAsync(MySqlDataPortCommunication communication, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(communication.ToConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task<List<object?[]>> SelectAllAsync(MySqlDataPortCommunication communication, string schema, string table, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(communication.ToConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(RelationalDatabaseTestHelper.ComposeSelectTableQuery('`', table, schema), connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        List<object?[]> rows = [];

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object?[reader.FieldCount];
            reader.GetValues(row!);
            rows.Add(Array.ConvertAll(row, v => v is DBNull ? null : v));
        }

        return rows;
    }

    internal static async Task<List<object?[]>> WaitForRowsAsync(MySqlDataPortCommunication communication, string schema, string table, int count, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        while (true)
        {
            var rows = await SelectAllAsync(communication, schema, table, timeout.Token);

            if (rows.Count >= count)
                return rows;

            await Task.Delay(200, timeout.Token);
        }
    }

    internal static async Task KillOtherConnectionsAsync(MySqlDataPortCommunication communication, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(communication.ToConnectionString());
        await connection.OpenAsync(cancellationToken);
        List<long> ids = [];

        await using (var select = new MySqlCommand("SELECT ID FROM information_schema.PROCESSLIST WHERE ID <> CONNECTION_ID() AND USER = SUBSTRING_INDEX(CURRENT_USER(), '@', 1) AND COMMAND <> 'Daemon';", connection))
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        foreach (var id in ids)
        {
            await using var kill = new MySqlCommand($"KILL {id};", connection);
            await kill.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
