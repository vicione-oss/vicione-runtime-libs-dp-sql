namespace ViciOne.Suite.DataPort;

internal record SchemaInfo(string Schema, string? Table, string? ColumnName, string? ColumnType, bool ColumnIsPrimaryKey, bool ColumnIsNotNull, bool ColumnIsAutoIncrement);
