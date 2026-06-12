namespace ViciOne.Suite.DataPort;

internal sealed class SqliteDataPortProperties(SqliteDataPortCommunication communication)
{
    internal string DataSource
    {
        get => communication.Filename;
        set => communication.Filename = value;
    }

    internal string? Password
    {
        get => communication.Password;
        set => communication.Password = value;
    }

    internal bool? Pooling
    {
        get => communication.Pooling;
        set => communication.Pooling = value;
    }
}
