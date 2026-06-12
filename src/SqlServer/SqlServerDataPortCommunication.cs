using ViciOne.ManagedEngine.Communication;

namespace ViciOne.Suite.DataPort;

[Communication("sqlserverdataport")]
public sealed record class SqlServerDataPortCommunication : RelationalDatabaseCommunication
{
    public string Host { get; set; } = string.Empty;
    public ushort? Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string? UserID { get; set; }
    public string? Password { get; set; }
    public int ConnectTimeout { get; set; } = 30;
}
