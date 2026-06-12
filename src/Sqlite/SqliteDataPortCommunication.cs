using ViciOne.ManagedEngine.Communication;

namespace ViciOne.Suite.DataPort;

[Communication("sqlitedataport")]
public sealed record class SqliteDataPortCommunication : RelationalDatabaseCommunication
{
    public string Filename { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool? Pooling { get; set; }
}
