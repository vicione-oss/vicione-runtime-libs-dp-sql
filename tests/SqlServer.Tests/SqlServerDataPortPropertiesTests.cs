using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqlServerDataPortProperties_Host
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string value)
    {
        SqlServerDataPortProperties properties = new(new() { Host = value, });

        properties.Host.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string value)
    {
        SqlServerDataPortCommunication communication = new();

        _ = new SqlServerDataPortProperties(communication)
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

public class SqlServerDataPortProperties_Port
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(ushort? communicationValue, ushort? propertyValue)
    {
        SqlServerDataPortProperties properties = new(new() { Port = communicationValue, });

        properties.Port.Should().Be(propertyValue);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(ushort? communicationValue, ushort? propertyValue)
    {
        SqlServerDataPortCommunication communication = new();

        _ = new SqlServerDataPortProperties(communication)
        {
            Port = propertyValue,
        };

        communication.Port.Should().Be(communicationValue);
    }

    public static TheoryData<ushort?, ushort?> Conversion() => new()
    {
        { null, null },
        { 1433, 1433 },
    };
}

public class SqlServerDataPortProperties_Database
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string value)
    {
        SqlServerDataPortProperties properties = new(new() { Database = value, });

        properties.Database.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string value)
    {
        SqlServerDataPortCommunication communication = new();

        _ = new SqlServerDataPortProperties(communication)
        {
            Database = value,
        };

        communication.Database.Should().Be(value);
    }

    public static TheoryData<string> Conversion() => new()
    {
        { new(string.Empty) },
        { new("localhost") },
    };
}

public class SqlServerDataPortProperties_UserID
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        SqlServerDataPortProperties properties = new(new() { UserID = value, });

        properties.UserID.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]

    public void Setter_sets_value(string? value)
    {
        SqlServerDataPortCommunication communication = new();

        _ = new SqlServerDataPortProperties(communication)
        {
            UserID = value,
        };

        communication.UserID.Should().Be(value);
    }

    public static TheoryData<string?> Conversion() => new()
    {
        { null! },
        { string.Empty },
        { "user" },
    };
}

public class SqlServerDataPortProperties_Password
{
    [Theory]
    [MemberData(nameof(Conversion))]
    public void Getter_returns_value(string? value)
    {
        SqlServerDataPortProperties properties = new(new() { Password = value, });

        properties.Password.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Conversion))]
    public void Setter_sets_value(string? value)
    {
        SqlServerDataPortCommunication communication = new();

        _ = new SqlServerDataPortProperties(communication)
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
