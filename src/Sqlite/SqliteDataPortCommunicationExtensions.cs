using Microsoft.Data.Sqlite;

namespace ViciOne.Suite.DataPort;

internal static class SqliteDataPortCommunicationExtensions
{
    internal static string ToConnectionString(this SqliteDataPortCommunication communication, bool allowCreate = false)
    {
        SqliteDataPortProperties properties = new(communication);
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = properties.DataSource,
            Mode = allowCreate ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly,
        };
        if (properties.Pooling is not null)
            builder.Pooling = properties.Pooling.Value;
        return builder.ConnectionString;
    }
}
