using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ViciOne.DataPort.Communication;

public static class AdoDotNetFunctions
{
    public static async Task EnsureConnectionAsync([NotNull] DbConnection? connection, CancellationToken cancellationToken)
    {
        if (connection is null)
            throw new InvalidOperationException("Command must have an connection.");
        if (connection.State == ConnectionState.Closed)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyCollection<DataRow>> ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(command.Connection, cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        using var dataTable = new DataTable();
        dataTable.Load(reader);
        var dataRows = dataTable.AsEnumerable().ToList();

        return dataRows;
    }

    public static async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(DbCommand command, Func<DbDataReader, T> read, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(command.Connection, cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<T> result = [];

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(read(reader));

        return result;
    }

    public static async Task ExecuteTransactionAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(command.Connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await command.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        command.Transaction = transaction;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        command.Transaction = null;
    }

    public static async Task ExecuteAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(command.Connection, cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
