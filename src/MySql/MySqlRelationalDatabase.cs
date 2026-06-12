using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using ViciOne.DataPort.Communication;

namespace ViciOne.Suite.DataPort;

internal sealed class MySqlRelationalDatabase(MySqlDataPortCommunication communication, ILogger logger) : RelationalDatabase(communication, logger, 64)
{
    private readonly MySqlDataPortCommunication _communication = communication;
    private readonly ILogger _logger = logger;
    private MySqlConnection? _connection;

    protected override async Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken)
    {
        _connection ??= new(_communication.ToConnectionString());
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
    protected override async Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases, IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken)
    {
        await AdoDotNetFunctions.EnsureConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);

        using var command = _connection.CreateCommand();

        if (missingSchemas.Count > 0 || missingTables.Count > 0)
        {
            command.CommandText = MySqlCommands.ComposeCreateStructureCommandText(missingSchemas, missingTables);
            await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    protected override async Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
    {
        await AdoDotNetFunctions.EnsureConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);

        using var command = _connection.CreateCommand();

        command.CommandText = MySqlCommands.ComposeSelectStructureCommandText();
        var result = await AdoDotNetFunctions.ExecuteReaderAsync(command, MySqlCommands.ReadSchemaInfo, cancellationToken).ConfigureAwait(false);

        return [new("default", result.ToSchemas())];
    }

    protected override IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes)
        => [new("default", [.. nodes.Unify()])];

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
            await PublishToMySqlAsync(_connection, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.PublishFailed(ex);
            return false;
        }

        return true;
    }

    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    private static async Task PublishToMySqlAsync(MySqlConnection connection, IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;

        foreach (var rowData in data.GroupBy(d => d.TableInfo))
        {
            var coulums = GetColumns(rowData);

            command.CommandText = MySqlCommands.ComposeInsertCommand(rowData.Key, coulums);

            foreach (var row in rowData)
            {
                command.Parameters.Clear();

                foreach (var (column, value) in row.Columns)
                    command.Parameters.Add(new(column.Name, value));

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ColumnInfo[] GetColumns(IEnumerable<RowData> data)
        => [.. data.SelectMany(t => t.Columns.Keys).Distinct()];

    protected override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
        => MySqlExtensions.CreateColumnInfos(nodes);
}
