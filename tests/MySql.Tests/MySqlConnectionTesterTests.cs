using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MySqlConnectionTester_TestConnectionAsync
{
    [Collection(MySqlTestEnvironment.Name)]
    public class Interoperability(MySqlTestSystem mySql)
    {
        [Fact]
        [Trait("Category", "Interoperability")]
        public Task Can_connect_to_server() => MySqlConnectionTester.TestConnectionAsync(mySql.Communication);
    }

    [Fact]
    public async Task Throws_if_test_connection_fails()
    {
        MySqlDataPortCommunication communication = new()
        {
            Server = "localhost",
            Port = 3306,
        };

        var act = FluentActions.Awaiting(() => MySqlConnectionTester.TestConnectionAsync(communication));

        await act.Should().ThrowAsync<DataPortConnectionFailedException>().WithMessage("*fail*connect*mysql*");
    }
}
