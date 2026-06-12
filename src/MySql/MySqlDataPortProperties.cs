namespace ViciOne.Suite.DataPort;

internal sealed class MySqlDataPortProperties(MySqlDataPortCommunication communication)
{
    internal string Server
    {
        get => communication.Server;
        set => communication.Server = value;
    }

    internal uint Port
    {
        get => communication.Port;
        set => communication.Port = (ushort)value;
    }

    internal string Database
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
