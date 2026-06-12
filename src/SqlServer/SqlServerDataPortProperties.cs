namespace ViciOne.Suite.DataPort;

internal sealed class SqlServerDataPortProperties(SqlServerDataPortCommunication communication)
{
    internal string Host
    {
        get => communication.Host;
        set => communication.Host = value;
    }

    internal ushort? Port
    {
        get => communication.Port;
        set => communication.Port = value;
    }

    internal string Database
    {
        get => communication.Database;
        set => communication.Database = value;
    }

    internal string? UserID
    {
        get => communication.UserID;
        set => communication.UserID = value;
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
