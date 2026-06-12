using System;
using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal static partial class SqlServerRelationalDatabaseLog
{
    [LoggerMessage(0, LogLevel.Error, "Cannot publish data to SQL-Server.")]
    internal static partial void PublishFailed(this ILogger logger, Exception exception);

    [LoggerMessage(1, LogLevel.Warning, "SingleDatabaseInstance node is active - automatic database creation is not supported. Ensure the database already exists before connecting.")]
    internal static partial void SingleDatabaseInstanceNoDatabaseCreation(this ILogger logger);
}
