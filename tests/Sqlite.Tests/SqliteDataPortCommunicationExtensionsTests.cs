using System;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteDataPortCommunicationExtensions_ToConnectionString
{
    [Fact]
    public void Sets_mandatory_parameters()
        => CreateCommunication().ToConnectionString().Should()
        .Contain("Data Source=file.db").And
        .Contain("Mode=ReadOnly").And
        .NotContain("Password").And
        .NotContain("Pooling");

    [Fact]
    public void Allows_creation()
        => CreateCommunication().ToConnectionString(true).Should().Contain("Mode=ReadWriteCreate");

    [Fact]
    public void Sets_Password_if_available()
        => CreateCommunication(c => c.Password = "pw").ToConnectionString().Should().Contain("Password=pw");

    [Fact]
    public void Sets_Pooling_if_available()
        => CreateCommunication(c => c.Pooling = true).ToConnectionString().Should().Contain("Pooling=True");

    private static SqliteDataPortCommunication CreateCommunication(Action<SqliteDataPortCommunication>? configure = default)
    {
        SqliteDataPortCommunication communication = new() { Filename = "file.db", };
        configure?.Invoke(communication);
        return communication;
    }
}
