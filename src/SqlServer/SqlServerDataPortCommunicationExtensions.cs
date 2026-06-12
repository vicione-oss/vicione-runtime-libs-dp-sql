using Microsoft.Data.SqlClient;

namespace ViciOne.Suite.DataPort;

internal static class SqlServerDataPortCommunicationExtensions
{
    internal static string ToConnectionString(this SqlServerDataPortCommunication communication)
    {
        SqlServerDataPortProperties properties = new(communication);
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = properties.Port.HasValue ? $"{properties.Host},{properties.Port}" : properties.Host,
            InitialCatalog = properties.Database,
            TrustServerCertificate = true,
            ConnectTimeout = communication.ConnectTimeout,
        };
        if (properties.UserID is not null)
            builder.UserID = properties.UserID;
        if (properties.Password is not null)
            builder.Password = properties.Password;
        return builder.ToString();
    }
}
