namespace ViciOne.Suite.DataPort;

public abstract record class RelationalDatabaseCommunication : DataPortCommunication
{
    public int BulkSize { get; init; } = 10_000;
    public uint MinimumPublishIntervalInMs { get; init; } = 3_000;
    public bool DisableStructureValidation { get; init; }
}
