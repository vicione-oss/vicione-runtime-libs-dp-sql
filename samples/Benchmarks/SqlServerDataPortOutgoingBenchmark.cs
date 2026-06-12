using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using ViciOne.ManagedEngine.ExternalCommunication;
using ViciOne.Suite.DataPort;

namespace Benchmarks;

[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.", Justification = "Must be public for BenchmarkDotNet")]
[ThroughputColumn(nameof(Schemas), nameof(TablesPerSchema), nameof(ColumnsPerTable), nameof(RowsPerTable))]
public class SqlServerDataPortOutgoingBenchmark : ISqlThroughput, IDisposable
{
    private SqlServerDataPortOutgoing? _communication;
    private bool _disposedValue;
    private readonly LoggerFactory _loggerFactory = new();
    private List<List<ExternalValue>>? _data;

    [GlobalSetup]
    public void Setup()
    {
        var nodes = SqlTreeBuilder.CreateTree(this);
        _data = SqlTreeBuilder.CreateTestData(nodes, RowsPerTable);
        SqlServerDataPortCommunication communication = new()
        {
            Nodes = nodes,
            BulkSize = BulkSize,
            MinimumPublishIntervalInMs = 10_000,
        };
        _ = new SqlServerDataPortProperties(communication)
        {
            Host = "",
            Port = 1433,
            Database = "",
            UserID = "",
            Password = "",
        };
        _communication = new(communication, _loggerFactory.CreateLogger<SqlServerDataPortOutgoing>());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _communication?.Dispose();
        _data?.Clear();
    }

    [Params(10)]
    public int Schemas { get; set; }
    [Params(10)]
    public int TablesPerSchema { get; set; }
    [Params(10)]
    public int ColumnsPerTable { get; set; }
    [Params(500)]
    public int RowsPerTable { get; set; }
    [Params(100_000)]
    public int BulkSize { get; set; }

    [Benchmark]
    [SuppressMessage("Naming", "CA1707:Bezeichner dürfen keine Unterstriche enthalten")]
    public async Task SqlServer_External_Outgoing_Async()
    {
        await _communication!.ConnectAsync(CancellationToken.None);
        foreach (var partition in _data!)
            await _communication.SendAsync(0, partition, CancellationToken.None);
        await _communication.DisconnectAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _communication?.Dispose();
                _loggerFactory.Dispose();
            }
            _disposedValue = true;
        }
    }
}
