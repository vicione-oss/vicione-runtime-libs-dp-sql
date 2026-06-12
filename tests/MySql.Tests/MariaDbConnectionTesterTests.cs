using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MariaDbConnectionTester_TestConnectionAsync
{
    [Collection(MariaDbTestEnvironment.Name)]
    public class Interoperability(MariaDbTestSystem mariaDb)
    {
        [Fact]
        [Trait("Category", "Interoperability")]
        public Task Can_connect_to_server() => MariaDbConnectionTester.TestConnectionAsync(mariaDb.Communication);
    }

    [Fact]
    public async Task Throws_if_test_connection_fails()
    {
        MySqlDataPortCommunication communication = new()
        {
            Server = "localhost",
            Port = 3306,
        };

        var act = FluentActions.Awaiting(() => MariaDbConnectionTester.TestConnectionAsync(communication));

        await act.Should().ThrowAsync<DataPortConnectionFailedException>().WithMessage("*fail*connect*mariadb*");
    }
}
