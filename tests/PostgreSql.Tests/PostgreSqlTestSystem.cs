using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ViciOne.Suite.DataPort;

[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public sealed class PostgreSqlTestSystem : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15.1").Build();

    internal PostgreSqlDataPortCommunication Communication { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        NpgsqlConnectionStringBuilder connectionString = new(_container.GetConnectionString());
        Communication.Host = connectionString.Host ?? string.Empty;
        Communication.Port = (ushort)connectionString.Port;
        Communication.Database = connectionString.Database;
        Communication.UserId = connectionString.Username;
        Communication.Password = connectionString.Password;
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
