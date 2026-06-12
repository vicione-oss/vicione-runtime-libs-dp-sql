using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ViciOne.DataPort.Communication;

namespace ViciOne.Suite.DataPort;

// Sqlite has no limitation on identifier length, but it is set to 128 to match SqlServer.
internal sealed class SqliteRelationalDatabase(SqliteDataPortCommunication communication, ILogger logger)
    : RelationalDatabase(communication, logger, 128)
{
    private readonly SqliteDataPortCommunication _communication = communication;
    private readonly ILogger _logger = logger;
    private SqliteConnection? _connection;

    protected override async Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> _, CancellationToken cancellationToken)
    {
        _connection ??= new(_communication.ToConnectionString(allowCreate: true));
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task CloseConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    protected override async Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> _1,
        IReadOnlyCollection<Database> _2, CancellationToken cancellationToken)
    {
        var commandText = SqliteCommands.ComposeMissingStructureCommandText(missingTables);
        using var command = new SqliteCommand(commandText, _connection);
        await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
    {
        using var command = new SqliteCommand(SqliteCommands.SelectAllTablesWithColumns, _connection);
        var result = await AdoDotNetFunctions.ExecuteReaderAsync(command, SqliteCommands.ReadTablesWithColumnsResult, cancellationToken).ConfigureAwait(false);
        return [result.Unify(),];
    }

    protected override IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes)
        => [nodes.Unify(),];

    protected override void Dispose(bool disposing)
    {
        _connection?.Dispose();
        base.Dispose(disposing);
    }

    protected override async ValueTask<bool> PublishAsync(IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        try
        {
            await AdoDotNetFunctions.EnsureConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);
            await PublishToSqliteAsync(_connection, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.PublishFailed(ex);
            return false;
        }

        return true;
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    private static async Task PublishToSqliteAsync(SqliteConnection connection, IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        foreach (var rowData in data.GroupBy(d => d.TableInfo))
        {
            var columns = GetColumns(rowData);
            command.CommandText = SqliteCommands.ComposeInsertRowsCommandText(rowData.Key.Table, columns);

            foreach (var row in rowData)
            {
                command.Parameters.Clear();

                foreach (var (column, value) in row.Columns)
                    command.Parameters.Add(new(column.Name, value));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        static IReadOnlyCollection<ColumnInfo> GetColumns(IEnumerable<RowData> data)
            => [.. data.SelectMany(t => t.Columns.Keys).Distinct()];
    }

    protected override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
        => SqliteExtensions.CreateColumnInfos(nodes);
}
