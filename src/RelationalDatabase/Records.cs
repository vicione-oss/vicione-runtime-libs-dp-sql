using System;
using System.Collections.Generic;

namespace ViciOne.Suite.DataPort;

public record Database(string Name, List<Schema> Schemas);
public record Schema(string Name, List<Table> Tables);
public record Table(string Name, List<Column> Columns);
public record Column(string Name, string Type, bool PrimaryKey, bool NotNull, bool AutoIncrement);

public record TableInfo(string Database, string Schema, string Table);
public record ColumnInfo(TableInfo TableInfo, string Name, bool IsJsonStructure, bool IsUnknownType, Type? Type);
public record RowData(TableInfo TableInfo, Dictionary<ColumnInfo, object?> Columns);
