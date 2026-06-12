using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MySqlDataPortProperties_Server
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string value)
    {
        MySqlDataPortProperties properties = new(new() { Server = value, });

        properties.Server.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string value)
    {
        MySqlDataPortCommunication communication = new();

        _ = new MySqlDataPortProperties(communication)
        {
            Server = value,
        };

        communication.Server.Should().Be(value);
    }

    public static TheoryData<string> Conversion() => new()
    {
        { new(string.Empty) },
        { new("localhost") },
    };
}

public class MySqlDataPortProperties_Port
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(ushort value, uint port)
    {
        MySqlDataPortProperties properties = new(new() { Port = value, });

        properties.Port.Should().Be(port);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(ushort value, uint port)
    {
        MySqlDataPortCommunication communication = new();

        _ = new MySqlDataPortProperties(communication)
        {
            Port = port,
        };

        communication.Port.Should().Be(value);
    }

    public static TheoryData<ushort, uint> Conversion() => new()
    {
        { 1883, 1883 },
    };
}

public class MySqlDataPortProperties_Database
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string value)
    {
        MySqlDataPortProperties properties = new(new() { Database = value, });

        properties.Database.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string value)
    {
        MySqlDataPortCommunication communication = new();

        _ = new MySqlDataPortProperties(communication)
        {
            Database = value,
        };

        communication.Database.Should().Be(value);
    }

    public static TheoryData<string> Conversion() => new()
    {
        { new(string.Empty) },
        { new("db") },
    };
}

public class MySqlDataPortProperties_UserId
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        MySqlDataPortProperties properties = new(new() { UserId = value, });

        properties.UserId.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string? value)
    {
        MySqlDataPortCommunication communication = new();

        _ = new MySqlDataPortProperties(communication)
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

public class MySqlDataPortProperties_Password
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        MySqlDataPortProperties properties = new(new() { Password = value, });

        properties.Password.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]
    public void Setter_sets_value(string? value)
    {
        MySqlDataPortCommunication communication = new();

        _ = new MySqlDataPortProperties(communication)
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
