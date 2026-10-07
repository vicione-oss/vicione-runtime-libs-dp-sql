using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ViciOne.Suite.DataPort;

internal static class SqliteTestQueries
{
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    internal static async Task<List<object?[]>> SelectAllAsync(string filename, string table, CancellationToken cancellationToken)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = filename,
            Pooling = false,
        };
        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqliteCommand(RelationalDatabaseTestHelper.ComposeSelectTableQuery('"', table), connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        List<object?[]> rows = [];

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(Array.ConvertAll(row, v => v is DBNull ? null : v));
        }

        return rows;
    }
}
