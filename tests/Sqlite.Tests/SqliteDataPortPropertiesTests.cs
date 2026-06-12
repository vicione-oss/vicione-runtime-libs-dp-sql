using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteDataPortProperties_DataSource
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string value)
    {
        SqliteDataPortProperties properties = new(new() { Filename = value, });

        properties.DataSource.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string value)
    {
        SqliteDataPortCommunication communication = new();

        _ = new SqliteDataPortProperties(communication)
        {
            DataSource = value,
        };

        communication.Filename.Should().Be(value);
    }

    public static TheoryData<string> Conversion() => new()
    {
        { new(string.Empty) },
        { new("localhost") },
    };
}

public class SqliteDataPortProperties_Password
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        SqliteDataPortProperties properties = new(new() { Password = value, });

        properties.Password.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]
    public void Setter_sets_value(string? value)
    {
        SqliteDataPortCommunication communication = new();

        _ = new SqliteDataPortProperties(communication)
        {
            Password = value,
        };

        communication.Password.Should().Be(value);
    }

    public static TheoryData<string?> Conversion() => new()
    {
        { null! },
        { string.Empty },
        { "pass" },
    };
}

public class SqliteDataPortProperties_Pooling
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(bool? value)
    {
        SqliteDataPortProperties properties = new(new() { Pooling = value, });

        properties.Pooling.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]
    public void Setter_sets_value(bool? value)
    {
        SqliteDataPortCommunication communication = new();

        _ = new SqliteDataPortProperties(communication)
        {
            Pooling = value,
        };

        communication.Pooling.Should().Be(value);
    }

    public static TheoryData<bool?> Conversion() => new()
    {
        { null! },
        { true },
        { false },
    };
}
