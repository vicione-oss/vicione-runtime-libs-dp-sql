namespace ViciOne.Suite.DataPort;

internal sealed class PostgreSqlDataPortProperties(PostgreSqlDataPortCommunication communication)
{
    internal string Host
    {
        get => communication.Host;
        set => communication.Host = value;
    }

    internal int Port
    {
        get => communication.Port;
        set => communication.Port = (ushort)value;
    }

    internal string? Database
    {
        get => communication.Database;
        set => communication.Database = value;
    }

    internal string? UserId
    {
        get => communication.UserId;
        set => communication.UserId = value;
    }

    internal string? Password
    {
        get => communication.Password;
        set => communication.Password = value;
    }

    internal int ConnectTimeout
    {
        get => communication.ConnectTimeout;
        set => communication.ConnectTimeout = value;
    }
}
