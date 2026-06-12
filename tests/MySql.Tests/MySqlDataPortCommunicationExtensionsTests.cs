using System;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MySqlDataPortCommunicationExtensions_ToConnectionString
{
    [Fact]
    public void Sets_mandatory_parameters()
        => CreateCommunication().ToConnectionString().Should()
        .Contain("Server=host").And
        .Contain("Port=1234").And
        .Contain("Database=db").And
        .NotContain("User ID").And
        .NotContain("Password");

    [Fact]
    public void Sets_UserID_if_available()
        => CreateCommunication(c => c.UserId = "user").ToConnectionString().Should().Contain("User ID=user");

    [Fact]
    public void Sets_Password_if_available()
        => CreateCommunication(c => c.Password = "pw").ToConnectionString().Should().Contain("Password=pw");

    [Fact]
    public void ConnectTimeout_defaults_to_30()
        => new MySqlDataPortCommunication().ConnectTimeout.Should().Be(30);

    [Fact]
    public void ConnectTimeout_is_included_in_connection_string()
        => CreateCommunication(c => c.ConnectTimeout = 60).ToConnectionString().Should().Contain("Connection Timeout=60");

    [Fact]
    public void Sets_ConnectTimeout_default_in_connection_string()
        => CreateCommunication().ToConnectionString().Should().Contain("Connection Timeout=30");

    private static MySqlDataPortCommunication CreateCommunication(Action<MySqlDataPortCommunication>? configure = default)
    {
        MySqlDataPortCommunication communication = new() { Server = "host", Port = 1234, Database = "db", };
        configure?.Invoke(communication);
        return communication;
    }
}
