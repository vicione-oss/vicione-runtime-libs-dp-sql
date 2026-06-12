using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace ViciOne.Suite.DataPort;

[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public sealed class SqlServerTestSystem : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();

    internal SqlServerDataPortCommunication Communication { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        SqlConnectionStringBuilder connectionString = new(_container.GetConnectionString());
        Communication.Host = connectionString.DataSource;
        Communication.Database = connectionString.InitialCatalog;
        Communication.UserID = connectionString.UserID;
        Communication.Password = connectionString.Password;
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
