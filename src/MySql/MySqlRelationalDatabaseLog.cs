using System;
using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal static partial class MySqlRelationalDatabaseLog
{
    [LoggerMessage(0, LogLevel.Error, "Cannot publish data to MySQL/MariaDB.")]
    internal static partial void PublishFailed(this ILogger logger, Exception exception);
}
