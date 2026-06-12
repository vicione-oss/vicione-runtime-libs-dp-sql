using ViciOne.ManagedEngine.Communication;

namespace ViciOne.Suite.DataPort;

[Communication("postgresqldataport")]
public sealed record class PostgreSqlDataPortCommunication : RelationalDatabaseCommunication
{
    public string Host { get; set; } = string.Empty;
    public ushort Port { get; set; }
    public string? Database { get; set; }
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public int ConnectTimeout { get; set; } = 30;
}
