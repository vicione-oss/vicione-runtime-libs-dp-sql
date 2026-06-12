using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class PostgreSqlDataPortProperties_Host
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string value)
    {
        PostgreSqlDataPortProperties properties = new(new() { Host = value, });

        properties.Host.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string value)
    {
        PostgreSqlDataPortCommunication communication = new();

        _ = new PostgreSqlDataPortProperties(communication)
        {
            Host = value,
        };

        communication.Host.Should().Be(value);
    }

    public static TheoryData<string> Conversion() => new()
    {
        { new(string.Empty) },
        { new("localhost") },
    };
}

public class PostgreSqlDataPortProperties_Port
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(ushort communicationValue, int propertyValue)
    {
        PostgreSqlDataPortProperties properties = new(new() { Port = communicationValue, });

        properties.Port.Should().Be(propertyValue);
    }

    [Theory]
    [MemberData(nameof(Conversion))]
    public void Setter_sets_value(ushort communicationValue, int propertyValue)
    {
        PostgreSqlDataPortCommunication communication = new();

        _ = new PostgreSqlDataPortProperties(communication)
        {
            Port = propertyValue,
        };

        communication.Port.Should().Be(communicationValue);
    }

    public static TheoryData<ushort, int> Conversion() => new()
    {
        { 1883, 1883 },
    };
}

public class PostgreSqlDataPortProperties_Database
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        PostgreSqlDataPortProperties properties = new(new() { Database = value, });

        properties.Database.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string? value)
    {
        PostgreSqlDataPortCommunication communication = new();

        _ = new PostgreSqlDataPortProperties(communication)
        {
            Database = value,
        };

        communication.Database.Should().Be(value);
    }

    public static TheoryData<string?> Conversion() => new()
    {
        { null! },
        { new(string.Empty) },
        { new("db") },
    };
}

public class PostgreSqlDataPortProperties_UserId
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        PostgreSqlDataPortProperties properties = new(new() { UserId = value, });

        properties.UserId.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string? value)
    {
        PostgreSqlDataPortCommunication communication = new();

        _ = new PostgreSqlDataPortProperties(communication)
        {
            UserId = value,
        };

        communication.UserId.Should().Be(value);
    }

    public static TheoryData<string?> Conversion() => new()
    {
        { null! },
        { string.Empty },
        { "user" },
    };
}

public class PostgreSqlDataPortProperties_Password
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        PostgreSqlDataPortProperties properties = new(new() { Password = value, });

        properties.Password.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]
    public void Setter_sets_value(string? value)
    {
        PostgreSqlDataPortCommunication communication = new();

        _ = new PostgreSqlDataPortProperties(communication)
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
