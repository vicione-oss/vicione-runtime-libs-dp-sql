using System;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class PostgreSqlDataPortCommunicationExtensions_ToConnectionString
{
    [Fact]
    public void Sets_mandatory_parameters()
        => CreateCommunication().ToConnectionString().Should()
            .Contain("Host=host").And
            .Contain("Port=1234").And
            .NotContain("Database").And
            .NotContain("Username").And
            .NotContain("Password");

    [Fact]
    public void Sets_Database_if_available()
        => CreateCommunication(c => c.Database = "db").ToConnectionString().Should().Contain("Database=db");

    [Fact]
    public void Sets_Username_if_available()
        => CreateCommunication(c => c.UserId = "user").ToConnectionString().Should().Contain("Username=user");

    [Fact]
    public void Sets_Password_if_available()
        => CreateCommunication(c => c.Password = "pw").ToConnectionString().Should().Contain("Password=pw");

    [Fact]
    public void ConnectTimeout_defaults_to_30()
        => new PostgreSqlDataPortCommunication().ConnectTimeout.Should().Be(30);

    [Fact]
    public void ConnectTimeout_is_included_in_connection_string()
        => CreateCommunication(c => c.ConnectTimeout = 60).ToConnectionString().Should().Contain("Timeout=60");

    [Fact]
    public void Sets_ConnectTimeout_default_in_connection_string()
        => CreateCommunication().ToConnectionString().Should().Contain("Timeout=30");

    private static PostgreSqlDataPortCommunication CreateCommunication(Action<PostgreSqlDataPortCommunication>? configure = default)
    {
        PostgreSqlDataPortCommunication communication = new() { Host = "host", Port = 1234, };
        configure?.Invoke(communication);
        return communication;
    }
}
