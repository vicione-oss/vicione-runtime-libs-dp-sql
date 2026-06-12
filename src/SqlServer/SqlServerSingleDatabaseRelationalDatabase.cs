using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using ViciOne.DataPort.Communication;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal sealed class SqlServerSingleDatabaseRelationalDatabase(SqlServerDataPortCommunication communication, ILogger logger)
    : SqlServerRelationalDatabase(communication, logger)
{
    protected override Task PrepareOpenAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken)
    {
        var databases = columnInfos.Select(c => c.TableInfo.Database).Distinct().ToList();

        if (databases.Count > 1)
        {
            throw new InvalidOperationException(
                $"More than one database detected but SingleDatabaseInstance node is active — only one database can be managed. Detected databases: {string.Join(", ", databases)}.");
        }

        if (databases.Count == 1)
            _communication.Database = databases[0];

        return Task.CompletedTask;
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    protected override async Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
    {
        using var command = await OpenCommandAsync(cancellationToken).ConfigureAwait(false);

        command.CommandText = SqlServerCommands.SelectAllSchemasWithInformation;
        var result = await AdoDotNetFunctions.ExecuteReaderAsync(command, SqlServerCommands.ReadSchemaInfo, cancellationToken).ConfigureAwait(false);
        return [new Database(_communication.Database, result.ToSchemas())];
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    protected override async Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases,
        IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken)
    {
        using var command = await OpenCommandAsync(cancellationToken).ConfigureAwait(false);

        if (missingDatabases.Count != 0)
            _logger.SingleDatabaseInstanceNoDatabaseCreation();

        foreach (var database in missingSchemas)
        {
            foreach (var schema in database.Schemas)
            {
                command.CommandText = SqlServerCommands.ComposeCreateSchemaCommandText(schema);
                await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }

        if (missingTables.Count != 0)
        {
            command.CommandText = SqlServerCommands.ComposeCreateTablesCommandText(missingTables, singleDatabase: true);
            await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task PublishToSqlServerAsync(SqlConnection connection, IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction);

        foreach (var rowData in data.GroupBy(d => d.TableInfo))
        {
            var dataTable = GetDataTable(rowData.Key);
            var rows = CreateDataRowsFromTemplate(dataTable, rowData).ToArray();
            bulkCopy.DestinationTableName = dataTable.TableName;
            await bulkCopy.WriteToServerAsync(rows, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
