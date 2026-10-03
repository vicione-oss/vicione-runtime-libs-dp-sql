using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ViciOne.DataPort.Communication;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class AdoDotNetFunctions_EnsureConnectionAsync
{
    [Fact]
    public async Task Does_not_reopen_an_open_connection_Async()
    {
        await using var connection = CreateConnection(ConnectionState.Open);

        await AdoDotNetFunctions.EnsureConnectionAsync(connection, TestContext.Current.CancellationToken);

        await connection.DidNotReceive().OpenAsync(Arg.Any<CancellationToken>());
        await connection.DidNotReceive().CloseAsync();
    }

    [Fact]
    public async Task Opens_a_closed_connection_Async()
    {
        await using var connection = CreateConnection(ConnectionState.Closed);

        await AdoDotNetFunctions.EnsureConnectionAsync(connection, TestContext.Current.CancellationToken);

        await connection.DidNotReceive().CloseAsync();
        await connection.Received(1).OpenAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ConnectionState.Broken)]
    [InlineData(ConnectionState.Connecting)]
    public async Task Closes_and_reopens_a_connection_that_is_not_open_Async(ConnectionState state)
    {
        await using var connection = CreateConnection(state);

        await AdoDotNetFunctions.EnsureConnectionAsync(connection, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            connection.CloseAsync();
            connection.OpenAsync(Arg.Any<CancellationToken>());
        });
    }

    private static DbConnection CreateConnection(ConnectionState state)
    {
        var connection = Substitute.For<DbConnection>();
        connection.State.Returns(state);
        return connection;
    }
}

public class AdoDotNetFunctions_CloseBrokenConnectionAsync
{
    [Fact]
    public async Task Closes_an_open_connection_Async()
    {
        await using var connection = Substitute.For<DbConnection>();
        connection.State.Returns(ConnectionState.Open);

        await AdoDotNetFunctions.CloseBrokenConnectionAsync(connection);

        await connection.Received(1).CloseAsync();
    }

    [Fact]
    public async Task Ignores_a_failing_close_Async()
    {
        await using var connection = Substitute.For<DbConnection>();
        connection.State.Returns(ConnectionState.Broken);
        connection.CloseAsync().ThrowsAsync(Substitute.For<DbException>());

        await AdoDotNetFunctions.CloseBrokenConnectionAsync(connection);

        await connection.Received(1).CloseAsync();
    }

    [Fact]
    public async Task Accepts_a_missing_connection_Async()
    {
        var act = () => AdoDotNetFunctions.CloseBrokenConnectionAsync(null);

        await act.Should().NotThrowAsync();
    }
}
