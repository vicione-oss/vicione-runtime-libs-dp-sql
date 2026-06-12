using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal static partial class RelationalDatabaseLog
{
    [LoggerMessage(0, LogLevel.Warning, "Cannot publish values. Items will be enqueued again.")]
    internal static partial void LogPublishFailed(this ILogger logger);

    [LoggerMessage(1, LogLevel.Error, "Cannot publish values. Queue will be cleared.")]
    internal static partial void LogPublishWithCompletedQueueFailed(this ILogger logger);
}
