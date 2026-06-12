using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqlServerConnectionTester_TestConnectionAsync
{
    [Collection(SqlServerTestEnvironment.Name)]
    public class Interoperability(SqlServerTestSystem sqlServer)
    {
        [Fact]
        [Trait("Category", "Interoperability")]
        public Task Can_connect_to_server() => SqlServerConnectionTester.TestConnectionAsync(sqlServer.Communication);
    }

    [Fact]
    public async Task Throws_if_test_connection_fails()
    {
        SqlServerDataPortCommunication communication = new()
        {
            Host = "localhost",
            Port = 1433,
        };

        var act = FluentActions.Awaiting(() => SqlServerConnectionTester.TestConnectionAsync(communication));

        await act.Should().ThrowAsync<DataPortConnectionFailedException>().WithMessage("*fail*connect*sqlserver*");
    }
}
