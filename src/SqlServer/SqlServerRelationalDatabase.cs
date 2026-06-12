using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ViciOne.DataPort.Communication;

namespace ViciOne.Suite.DataPort;

internal abstract class SqlServerRelationalDatabase(SqlServerDataPortCommunication communication, ILogger logger) : RelationalDatabase(communication, logger, 128)
{
    internal const string SingleDatabaseInstanceNodeId = "SqlServer-SingleDatabaseInstance";

    protected readonly SqlServerDataPortCommunication _communication = communication;
    protected readonly ILogger _logger = logger;
    private SqlConnection? _connection;
    private readonly Dictionary<TableInfo, DataTable> _tables = [];
    private string _connectionString = string.Empty;

    internal static SqlServerRelationalDatabase Create(SqlServerDataPortCommunication communication, ILogger logger)
        => communication.DesignId == SingleDatabaseInstanceNodeId
            ? new SqlServerSingleDatabaseRelationalDatabase(communication, logger)
            : new SqlServerMultiDatabaseRelationalDatabase(communication, logger);

    protected sealed override async Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken)
    {
        await PrepareOpenAsync(columnInfos, cancellationToken).ConfigureAwait(false);
        FillTablesCache();
        await ConnectAsync().ConfigureAwait(false);

        void FillTablesCache()
        {
            foreach (var tableColumns in columnInfos.GroupBy(c => c.TableInfo))
                _tables.Add(tableColumns.Key, CreateDataTableTemplate(tableColumns.Key, tableColumns));
        }

        async Task ConnectAsync()
        {
            if (_connection is null)
            {
                _connectionString = _communication.ToConnectionString();
                _connection = new(_connectionString);
                await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                SqlConnectionPoolTracker.Register(_connectionString);
            }
        }
    }

    protected virtual Task PrepareOpenAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static DataTable CreateDataTableTemplate(TableInfo tableInfo, IEnumerable<ColumnInfo> columnInfos)
    {
        DataTable dataTable = new(SqlServerCommands.ComposeTableNameTwoPart(tableInfo));

        foreach (var columnInfo in columnInfos)
        {
            if (columnInfo.Type != null)
                dataTable.Columns.Add(columnInfo.Name, columnInfo.Type);
            else
                dataTable.Columns.Add(columnInfo.Name);
        }

        return dataTable;
    }

    protected sealed override async Task CloseConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                SqlConnectionPoolTracker.Unregister(_connectionString, _connection);
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
        }

        foreach (var table in _tables.Values)
            table.Dispose();
        _tables.Clear();
    }

    protected async Task<SqlCommand> OpenCommandAsync(CancellationToken cancellationToken)
    {
        await AdoDotNetFunctions.EnsureConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);
        return _connection.CreateCommand();
    }

    protected DataTable GetDataTable(TableInfo tableInfo) => _tables[tableInfo];

    protected sealed override async ValueTask<bool> PublishAsync(IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        try
        {
            await AdoDotNetFunctions.EnsureConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);
            await PublishToSqlServerAsync(_connection, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.PublishFailed(ex);
            return false;
        }

        return true;
    }

    protected abstract Task PublishToSqlServerAsync(SqlConnection connection, IEnumerable<RowData> data, CancellationToken cancellationToken);

    protected sealed override IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes)
        => nodes.Unify();

    protected override void Dispose(bool disposing)
    {
        _connection?.Dispose();
        foreach (var table in _tables.Values)
            table.Dispose();
        _tables.Clear();
        base.Dispose(disposing);
    }

    internal static IEnumerable<DataRow> CreateDataRowsFromTemplate(DataTable dataTable, IEnumerable<RowData> data)
    {
        foreach (var rowData in data)
        {
            var row = dataTable.NewRow();

            foreach (var column in rowData.Columns)
                row[column.Key.Name] = column.Value;

            yield return row;
        }
    }

    protected sealed override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
        => SqlServerExtensions.CreateColumnInfos(nodes);
}
