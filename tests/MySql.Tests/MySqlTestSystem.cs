using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using MySqlConnector;
using Testcontainers.MySql;
using Xunit;

namespace ViciOne.Suite.DataPort;

[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public sealed class MySqlTestSystem : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.0").WithUsername("root").Build();

    internal MySqlDataPortCommunication Communication { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        MySqlConnectionStringBuilder connectionString = new(_container.GetConnectionString());
        Communication.Server = connectionString.Server;
        Communication.Port = (ushort)connectionString.Port;
        Communication.Database = connectionString.Database;
        Communication.UserId = connectionString.UserID;
        Communication.Password = connectionString.Password;
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
