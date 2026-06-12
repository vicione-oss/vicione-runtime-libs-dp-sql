namespace ViciOne.Suite.DataPort;

internal record TablesWithColumnsResult(string Table, string ColumnName, string ColumnType, bool ColumnIsPrimaryKey, bool ColumnIsNotNull);
