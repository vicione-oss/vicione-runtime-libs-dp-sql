using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using ViciOne.ManagedEngine.ExternalCommunication;
using ViciOne.Suite.DataPort;

namespace Benchmarks;

[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.", Justification = "Must be public for BenchmarkDotNet")]
[ThroughputColumn(nameof(TablesPerSchema), nameof(ColumnsPerTable), nameof(RowsPerTable))]
public class SqliteDataPortOutgoingBenchmark : ISqlThroughput, IDisposable
{
    private readonly string _dataSource = @"test.db";
    private readonly LoggerFactory _loggerFactory = new();
    private List<List<ExternalValue>>? _data;
    private SqliteDataPortOutgoing? _communication;
    private bool _disposedValue;

    [GlobalSetup]
    public void Setup()
    {
        File.Delete(_dataSource);

        var nodes = SqlTreeBuilder.CreateTreeStartingWithTables(this);
        _data = SqlTreeBuilder.CreateTestData(nodes, RowsPerTable);
        SqliteDataPortCommunication communication = new()
        {
            Nodes = nodes,
            BulkSize = BulkSize,
            MinimumPublishIntervalInMs = 10_000,
        };
        _ = new SqliteDataPortProperties(communication)
        {
            DataSource = _dataSource,
        };
        _communication = new SqliteDataPortOutgoing(communication, _loggerFactory.CreateLogger<SqliteDataPortOutgoing>());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _communication?.Dispose();
        _data?.Clear();
    }

    public int Schemas { get; }
    [Params(100)]
    public int TablesPerSchema { get; set; }
    [Params(10)]
    public int ColumnsPerTable { get; set; }
    [Params(1000)]
    public int RowsPerTable { get; set; }
    [Params(25_000)]
    public int BulkSize { get; set; }

    [Benchmark]
    [SuppressMessage("Naming", "CA1707:Bezeichner dürfen keine Unterstriche enthalten")]
    public async Task Sqlite_External_Outgoing_Async()
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
