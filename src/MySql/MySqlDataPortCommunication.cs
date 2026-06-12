using ViciOne.ManagedEngine.Communication;

namespace ViciOne.Suite.DataPort;

[Communication("mariadbdataport")]
[Communication("mysqldataport")]
public sealed record class MySqlDataPortCommunication : RelationalDatabaseCommunication
{
    public string Server { get; set; } = string.Empty;
    public ushort Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public int ConnectTimeout { get; set; } = 30;
}
