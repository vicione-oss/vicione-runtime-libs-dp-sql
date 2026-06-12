using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using ViciOne.DataPort.Communication;

namespace ViciOne.Suite.DataPort;

internal sealed class PostgreSqlRelationalDatabase(PostgreSqlDataPortCommunication communication, ILogger logger) : RelationalDatabase(communication, logger, 63)
{
    private readonly PostgreSqlDataPortCommunication _communication = communication;
    private readonly ILogger _logger = logger;
    private NpgsqlConnection? _connection;

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

        if (missingSchemas.Count != 0 || missingTables.Count != 0)
        {
            command.CommandText = PostgreSqlCommands.ComposeCreateStructureCommandText(missingSchemas, missingTables);
            await AdoDotNetFunctions.ExecuteTransactionAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
    {
        await AdoDotNetFunctions.EnsureConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);

        using var command = _connection.CreateCommand();

        List<Database> databases = [];

        command.CommandText = PostgreSqlCommands.SelectAllSchemasWithInformation;
        var result = await AdoDotNetFunctions.ExecuteReaderAsync(command, PostgreSqlCommands.ReadSchemaInfo, cancellationToken).ConfigureAwait(false);

        databases.Add(new Database("default", result.ToSchemas()));

        return databases;
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
            await PublishToPostgreSqlAsync(_connection, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.PublishFailed(ex);

            return false;
        }

        return true;
    }

    private static async Task PublishToPostgreSqlAsync(NpgsqlConnection connection, IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var groupedBySchema = data.GroupBy(schema => schema.TableInfo.Schema);

        foreach (var schemaData in groupedBySchema)
        {
            foreach (var rowData in schemaData.GroupBy(d => d.TableInfo))
            {
                var columns = GetColumns(rowData);

                await using (var writer = await connection.BeginBinaryImportAsync(
                    PostgreSqlCommands.ComposeCopyCommand(
                        rowData.Key.Table,
                        rowData.Key.Schema,
                        columns),
                    cancellationToken))
                {
                    foreach (var row in rowData)
                    {
                        await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);

                        foreach (var column in columns)
                        {
                            if (row.Columns.TryGetValue(column, out var value))
                                await writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
                            else
                                await writer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        static IReadOnlyCollection<ColumnInfo> GetColumns(IEnumerable<RowData> data)
            => [.. data.SelectMany(t => t.Columns.Keys).Distinct()];
    }

    protected override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
        => PostgreSqlExtensions.CreateColumnInfos(nodes);
}
