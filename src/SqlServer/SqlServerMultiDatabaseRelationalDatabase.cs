using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using ViciOne.DataPort.Communication;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal sealed class SqlServerMultiDatabaseRelationalDatabase(SqlServerDataPortCommunication communication, ILogger logger)
    : SqlServerRelationalDatabase(communication, logger)
{
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    protected override async Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
    {
        using var command = await OpenCommandAsync(cancellationToken).ConfigureAwait(false);

        command.CommandText = SqlServerCommands.SelectAllDatabases;
        var databaseNames = await AdoDotNetFunctions.ExecuteReaderAsync(command, SqlServerCommands.ReadDatabase, cancellationToken).ConfigureAwait(false);
        List<Database> databases = [];

        foreach (var database in databaseNames)
        {
            command.CommandText = SqlServerCommands.ComposeSelectStructureCommandText(database);
            var result = await AdoDotNetFunctions.ExecuteReaderAsync(command, SqlServerCommands.ReadSchemaInfo, cancellationToken).ConfigureAwait(false);
            databases.Add(new Database(database, result.ToSchemas()));
        }

        return databases;
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    protected override async Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases,
        IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken)
    {
        using var command = await OpenCommandAsync(cancellationToken).ConfigureAwait(false);

        if (missingDatabases.Count != 0)
        {
            command.CommandText = SqlServerCommands.ComposeCreateDatabasesCommandText(missingDatabases);
            await AdoDotNetFunctions.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }

        foreach (var database in missingSchemas)
        {
            command.CommandText = SqlServerCommands.ComposeUseDatabaseCommandText(database);
            await AdoDotNetFunctions.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);

            foreach (var schema in database.Schemas)
            {
                command.CommandText = SqlServerCommands.ComposeCreateSchemaCommandText(schema);
                await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }

        if (missingTables.Count != 0)
        {
            command.CommandText = SqlServerCommands.ComposeCreateTablesCommandText(missingTables);
            await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task PublishToSqlServerAsync(SqlConnection connection, IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction);

        foreach (var dbGroup in data.GroupBy(d => d.TableInfo.Database))
        {
            await connection.ChangeDatabaseAsync(dbGroup.Key, cancellationToken).ConfigureAwait(false);

            foreach (var rowData in dbGroup.GroupBy(d => d.TableInfo))
            {
                var dataTable = GetDataTable(rowData.Key);
                var rows = CreateDataRowsFromTemplate(dataTable, rowData).ToArray();
                bulkCopy.DestinationTableName = dataTable.TableName;
                await bulkCopy.WriteToServerAsync(rows, cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
