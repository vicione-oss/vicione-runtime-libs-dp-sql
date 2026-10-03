using Microsoft.Extensions.Logging;

namespace ViciOne.Suite.DataPort;

internal static partial class SqlServerRelationalDatabaseLog
{
    [LoggerMessage(1, LogLevel.Warning, "SingleDatabaseInstance node is active - automatic database creation is not supported. Ensure the database already exists before connecting.")]
    internal static partial void SingleDatabaseInstanceNoDatabaseCreation(this ILogger logger);
}
