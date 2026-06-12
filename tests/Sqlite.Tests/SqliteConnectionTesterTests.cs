using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteConnectionTester_TestConnectionAsync
{
    [Fact]
    public async Task Can_connect_to_server()
    {
        using SqliteConnection connection = new($"Data Source={nameof(Can_connect_to_server)}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.CloseAsync();

        SqliteDataPortCommunication communication = new()
        {
            Filename = nameof(Can_connect_to_server),
        };

        await SqliteConnectionTester.TestConnectionAsync(communication);
    }

    [Fact]
    public async Task Throws_if_test_connection_fails()
    {
        SqliteDataPortCommunication communication = new()
        {
            Filename = nameof(Throws_if_test_connection_fails),
        };

        var act = FluentActions.Awaiting(() => SqliteConnectionTester.TestConnectionAsync(communication));

        await act.Should().ThrowAsync<DataPortConnectionFailedException>().WithMessage("*fail*connect*sqlite*");
    }
}
