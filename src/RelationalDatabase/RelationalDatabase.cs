using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ViciOne.DataPort.Communication;
using ViciOne.ManagedEngine.ExternalCommunication;

namespace ViciOne.Suite.DataPort;

public abstract class RelationalDatabase : IDisposable
{
    private const int QueueSize = 100_000;

    internal const string MeterName = "ViciOne.Suite.DataPort.Sql";
    internal const string RowsDroppedInstrumentName = "vicione.dataport.sql.rows_dropped";

    private readonly RelationalDatabaseCommunication _dataPortCommunication;
    private readonly ILogger _logger;
    private Dictionary<string, ColumnInfo> _channelColumnInfo = [];
    private readonly Dictionary<string, INode[]> _routes;
    private readonly Dictionary<string, Guid> _channelColumnNodes;
    private readonly ManagedQueuePipeline<RowData>? _queue;
    private readonly HashSet<RowData> _handledRows = new(ReferenceEqualityComparer.Instance);
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _rowsDropped;

    protected RelationalDatabase(RelationalDatabaseCommunication dataPortCommunication, ILogger logger, int maxIdentifierLength = int.MaxValue)
    {
        ValidateNodeNames(dataPortCommunication.Nodes, maxIdentifierLength);

        var nodeParentsCache = dataPortCommunication.Nodes.CreateNodeParentsCache();
        var routes = dataPortCommunication.Nodes
            .Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId)
            .SelectMany(GetRoutes)
            .ToArray();

        _channelColumnNodes = routes.ToDictionary(r => r.AffectedChannel, r => r.Node.Id);
        _routes = routes.ToDictionary(r => r.AffectedChannel, r => r.Route);
        _dataPortCommunication = dataPortCommunication;
        _logger = logger;
        _rowsDropped = _meter.CreateCounter<long>(RowsDroppedInstrumentName, "{row}", "Rows the database rejected and the DataPort dropped.");
        _queue ??= new ManagedQueuePipeline<RowData>(
            QueueSize,
            string.Empty,
            _dataPortCommunication.BulkSize,
            _logger,
            _logger.LogPublishFailed,
            _logger.LogPublishWithCompletedQueueFailed,
            minimumPublishInterval: TimeSpan.FromMilliseconds(_dataPortCommunication.MinimumPublishIntervalInMs));

        IEnumerable<(string AffectedChannel, INode Node, INode[] Route)> GetRoutes(INode node)
        {
            var routes = node.GetSuccessorRoutes(nodeParentsCache);

            if (routes.Count == 0)
            {
                foreach (var channel in node.AffectedChannels)
                    yield return (channel, node, []);
                yield break;
            }

            foreach (var route in routes)
            {
                foreach (var channel in route.Last().AffectedChannels)
                    yield return (channel, node, route);
            }
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var nodeColumnInfo = CreateColumnInfos(_dataPortCommunication.Nodes);
        _channelColumnInfo = _channelColumnNodes.ToDictionary(cc => cc.Key, cc => nodeColumnInfo[cc.Value]);

        await OpenConnectionAsync(_channelColumnInfo.Values, cancellationToken).ConfigureAwait(false);

        var expected = GetExpectedStructure(_dataPortCommunication.Nodes);
        var current = await GetExistingStructureAsync(cancellationToken).ConfigureAwait(false);

        await EnsureStructureAsync(expected, current, cancellationToken).ConfigureAwait(false);

        _queue?.Start(PublishAsync, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_queue is not null)
            await _queue.Complete();

        _handledRows.Clear();

        await CloseConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureStructureAsync(IReadOnlyCollection<Database> expected, IReadOnlyCollection<Database> current, CancellationToken cancellationToken)
    {
        if (_dataPortCommunication.DisableStructureValidation)
            return;

        if (DatabaseComparer.TryGetMissingStructure(current, expected, out var missingTables, out var missingDatabases, out var missingSchemas))
            await CreateMissingStructureAsync(missingTables, missingDatabases, missingSchemas, cancellationToken).ConfigureAwait(false);

        if (DatabaseComparer.ExistingTablesAreUnequal(current, expected, out var differences))
            throw new InvalidOperationException(ComposeMessage(differences));
    }

    public void Send(IReadOnlyCollection<ExternalValue> values)
    {
        foreach (var row in PrepareRowData(values, _channelColumnInfo, _routes))
            _queue?.Append(row);
    }

    internal async ValueTask<bool> PublishAsync(IEnumerable<RowData> data, CancellationToken cancellationToken)
    {
        List<RowData> rows = [.. data.Where(row => !_handledRows.Remove(row))];

        if (rows.Count == 0)
            return true;

        try
        {
            await PublishRowsAsync(rows, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (IsRowRejected(ex))
        {
            _logger.LogBatchRejected(rows.Count, ex);
        }
        catch (Exception ex)
        {
            return await RetryLaterAsync(ex).ConfigureAwait(false);
        }

        return await IsolateRejectedRowsAsync(rows, cancellationToken).ConfigureAwait(false);
    }

    // Halving the batch finds a few rejected rows in a few round trips instead of one per row. If the database
    // becomes unreachable on the way, the queue retries the whole batch; the rows written or dropped until then
    // are remembered so that the retry skips them instead of writing them twice.
    private async ValueTask<bool> IsolateRejectedRowsAsync(List<RowData> rows, CancellationToken cancellationToken)
    {
        Stack<ArraySegment<RowData>> pending = new();
        List<RowData> handled = [];
        PushHalves(pending, rows.ToArray());

        while (pending.TryPop(out var segment))
        {
            try
            {
                await PublishRowsAsync(segment, cancellationToken).ConfigureAwait(false);
                handled.AddRange(segment);
            }
            catch (Exception ex) when (IsRowRejected(ex))
            {
                if (segment.Count > 1)
                {
                    PushHalves(pending, segment);
                    continue;
                }

                DropRow(segment[0], ex);
                handled.Add(segment[0]);
            }
            catch (Exception ex)
            {
                _handledRows.UnionWith(handled);
                return await RetryLaterAsync(ex).ConfigureAwait(false);
            }
        }

        return true;
    }

    private static void PushHalves(Stack<ArraySegment<RowData>> pending, ArraySegment<RowData> rows)
    {
        var half = rows.Count / 2;
        pending.Push(rows[half..]);
        pending.Push(rows[..half]);
    }

    private void DropRow(RowData row, Exception exception)
    {
        _logger.LogRowDropped(row.TableInfo.Schema, row.TableInfo.Table, exception);
        _rowsDropped.Add(1, new TagList
        {
            { "db.collection.name", $"{row.TableInfo.Schema}.{row.TableInfo.Table}" },
        });
    }

    private async ValueTask<bool> RetryLaterAsync(Exception exception)
    {
        _logger.LogPublishDeferred(exception);
        await AdoDotNetFunctions.CloseBrokenConnectionAsync(Connection).ConfigureAwait(false);
        return false;
    }

    // Only an error caused by the values of a row drops rows. Everything else - a lost connection, a timeout,
    // a lock, a missing table or permission - keeps the rows queued until the database accepts them again.
    private bool IsRowRejected(Exception exception)
        => exception is ArgumentException or OverflowException or InvalidCastException or FormatException
            || IsRejectedByDatabase(exception);

    internal static IEnumerable<RowData> PrepareRowData(IReadOnlyCollection<ExternalValue> values, Dictionary<string, ColumnInfo> channelColumns, Dictionary<string, INode[]> routes)
    {
        return values
            .GroupBy(v => channelColumns[v.Channel])
            .GroupBy(e => e.Key.TableInfo)
            .Select(tg => new RowData(tg.Key, tg.ToDictionary(cg => cg.Key, cg => CreateValue(cg))));

        object? CreateValue(IGrouping<ColumnInfo, ExternalValue> grouping)
        {
            if (grouping.Key.IsJsonStructure)
            {
                return NodeValueFactory.CreateJsonStructure(routes, [.. grouping]).IncludeMeta().ToString();
            }
            else
            {
                var value = grouping.First().Value;

                if (value is null)
                    return DBNull.Value;
                else if (grouping.Key.IsUnknownType)
                    return JsonSerializer.Serialize(value);
                return value;
            }
        }
    }

    internal static void ValidateNodeNames(IReadOnlyCollection<INode> nodes, int maxIdentifierLength = int.MaxValue)
    {
        var inappropriateNodes = nodes
            .Where(n => ContainsInappropriateSign(n.Name) || n.Name.Length > maxIdentifierLength)
            .ToArray();

        if (inappropriateNodes.Length != 0)
            throw new InvalidOperationException($"Some node names are inappropriate. ({string.Join(", ", inappropriateNodes.Select(n => $"{n.Name}({n.Id})"))})");
    }

    internal static bool ContainsInappropriateSign(string name)
        => name.AsSpan().IndexOfAnyInRange('\x00', '\x1F') != -1 || name.Contains('\x7F', StringComparison.Ordinal);

    internal static string ComposeMessage(IReadOnlyCollection<Database> databases)
    {
        var names = databases.SelectMany(db =>
            db.Schemas.SelectMany(s =>
                s.Tables.SelectMany(t =>
                    t.Columns.Select(c =>
                        $"{db.Name}.{s.Name}.{t.Name}.{c.Name}"))));
        return $"There are breaking changes between the existing database and the expected one. ({string.Join(", ", names)})";
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _queue?.Dispose();
            _meter.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken);
    protected abstract Task CloseConnectionAsync(CancellationToken cancellationToken);
    protected virtual DbConnection? Connection => null;

    protected virtual bool IsRejectedByDatabase(Exception exception) => false;

    protected abstract Task PublishRowsAsync(IReadOnlyCollection<RowData> rows, CancellationToken cancellationToken);
    protected abstract Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken);
    protected abstract IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes);
    protected abstract Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases,
        IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken);
    protected abstract Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes);
}
