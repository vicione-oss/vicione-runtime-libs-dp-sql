using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class PostgreSqlConnectionTester_TestConnectionAsync
{
    [Collection(PostgreSqlTestEnvironment.Name)]
    public class Interoperability(PostgreSqlTestSystem postgreSql)
    {
        [Fact]
        [Trait("Category", "Interoperability")]
        public Task Can_connect_to_server() => PostgreSqlConnectionTester.TestConnectionAsync(postgreSql.Communication);
    }

    [Fact]
    public async Task Throws_if_test_connection_fails()
    {
        PostgreSqlDataPortCommunication communication = new()
        {
            Host = "localhost",
            Port = 5432,
            Database = "notexisting",
        };

        var act = FluentActions.Awaiting(() => PostgreSqlConnectionTester.TestConnectionAsync(communication));

        await act.Should().ThrowAsync<DataPortConnectionFailedException>().WithMessage("*fail*connect*postgresql*");
    }
}
