using System;
using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal static partial class RelationalDatabaseLog
{
    [LoggerMessage(0, LogLevel.Warning, "Cannot publish values. Items will be enqueued again.")]
    internal static partial void LogPublishFailed(this ILogger logger);

    [LoggerMessage(1, LogLevel.Error, "Cannot publish values. Queue will be cleared.")]
    internal static partial void LogPublishWithCompletedQueueFailed(this ILogger logger);

    [LoggerMessage(2, LogLevel.Warning, "The database did not accept the values. They stay queued and are published again.")]
    internal static partial void LogPublishDeferred(this ILogger logger, Exception exception);

    [LoggerMessage(3, LogLevel.Warning, "The database rejected a batch of {Count} rows. The rejected rows are isolated.")]
    internal static partial void LogBatchRejected(this ILogger logger, int count, Exception exception);

    [LoggerMessage(4, LogLevel.Error, "The database rejected a row for {Schema}.{Table}. The row is dropped.")]
    internal static partial void LogRowDropped(this ILogger logger, string schema, string table, Exception exception);
}
