namespace Benchmarks;

internal interface ISqlThroughput
{
    int Schemas { get; }
    int TablesPerSchema { get; }
    int ColumnsPerTable { get; }
    int RowsPerTable { get; }
}
