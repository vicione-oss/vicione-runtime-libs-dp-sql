using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace ViciOne.Suite.DataPort;

internal static class SqlServerExtensions
{
    private static readonly Dictionary<Type, SqlDbType> s_typeMapping = new()
    {
        { typeof(bool), SqlDbType.Bit },
        { typeof(short), SqlDbType.SmallInt },
        { typeof(int), SqlDbType.Int },
        { typeof(long), SqlDbType.BigInt },
        { typeof(ushort), SqlDbType.Int },
        { typeof(uint), SqlDbType.BigInt },
        { typeof(ulong), SqlDbType.BigInt }, // DB does not support this type, stack overflow possible
        { typeof(float), SqlDbType.Real },
        { typeof(double), SqlDbType.Float },
        { typeof(decimal), SqlDbType.Decimal },
        { typeof(DateTime), SqlDbType.DateTime },
        { typeof(TimeSpan), SqlDbType.Time },
        { typeof(Guid), SqlDbType.UniqueIdentifier },
        { typeof(byte), SqlDbType.TinyInt },
        { typeof(byte[]), SqlDbType.VarBinary },
        { typeof(char), SqlDbType.Text },
        { typeof(string), SqlDbType.Text },
    };

    internal static bool IsSupportedType(Type type)
        => s_typeMapping.ContainsKey(type);

    [SuppressMessage("Globalization", "CA1308:Zeichenfolgen in Großbuchstaben normalisieren")]
    internal static string ConvertType(Type type)
    {
        if (type == typeof(decimal))
            return "decimal(38,30)"; // max precision

        if (type == typeof(byte[]))
            return "varbinary(MAX)";

        return (s_typeMapping.TryGetValue(Nullable.GetUnderlyingType(type) ?? type, out var dbType) ? dbType : SqlDbType.Text)
            .ToString().ToLower(CultureInfo.InvariantCulture);
    }

    internal static IReadOnlyCollection<Database> Unify(this IReadOnlyCollection<INode> nodes)
    {
        var databaseNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.DatabaseNodeId);
        var schemaNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId);
        var tableNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.TableNodeId);
        var columnNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId);
        var nodeParentsCache = nodes.CreateNodeParentsCache();

        List<Database> databases = [];
        foreach (var database in databaseNodes)
        {
            List<Schema> schemas = [];
            foreach (var schema in schemaNodes.Where(sn => sn.ParentId == database.Id))
            {
                List<Table> tables = [];
                foreach (var table in tableNodes.Where(sn => sn.ParentId == schema.Id))
                {
                    List<Column> columns = [];
                    foreach (var column in columnNodes.Where(sn => sn.ParentId == table.Id))
                    {
                        var isJsonColumn = column.IsJsonColumn(nodeParentsCache);
                        var primaryKey = column.GetPropertyValueOrDefault(RelationalDatabaseNodeProperty.PrimaryKey, false);
                        var autoIncrement = column.GetPropertyValueOrDefault(RelationalDatabaseNodeProperty.AutoIncrement, false);
                        var notNull = column.GetPropertyValueOrDefault(RelationalDatabaseNodeProperty.NotNull, false) || primaryKey || autoIncrement;
                        columns.Add(new(
                            column.Name,
                            ConvertType(isJsonColumn ? typeof(string) : column.ValueType ?? throw new InvalidOperationException("The column must have a type.")),
                            primaryKey,
                            notNull,
                            autoIncrement));
                    }

                    tables.Add(new(table.Name, columns));
                }

                schemas.Add(new(schema.Name, tables));
            }

            databases.Add(new(database.Name, schemas));
        }

        return databases;
    }

    internal static Dictionary<Guid, ColumnInfo> CreateColumnInfos(this IReadOnlyCollection<INode> nodes)
    {
        var databaseNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.DatabaseNodeId);
        var schemaNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId);
        var tableNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.TableNodeId);
        var columnNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId);
        var nodeParentsCache = nodes.CreateNodeParentsCache();
        Dictionary<Guid, ColumnInfo> columns = [];

        foreach (var database in databaseNodes)
        {
            foreach (var schema in schemaNodes.Where(sn => sn.ParentId == database.Id))
            {
                foreach (var table in tableNodes.Where(sn => sn.ParentId == schema.Id))
                {
                    var tableInfo = new TableInfo(database.Name, schema.Name, table.Name);

                    foreach (var column in columnNodes.Where(sn => sn.ParentId == table.Id))
                    {
                        columns.Add(column.Id, new(
                            tableInfo,
                            column.Name,
                            column.IsJsonColumn(nodeParentsCache),
                            column.ValueType is not null && !IsSupportedType(column.ValueType),
                            column.ValueType));
                    }
                }
            }
        }

        return columns;
    }

    internal static List<Schema> ToSchemas(this IReadOnlyCollection<SchemaInfo> dataRows)
    {
        List<Schema> schemas = [];

        foreach (var rowsBySchema in dataRows.GroupBy(d => d.Schema))
        {
            List<Table> tables;
            var rows = rowsBySchema.ToArray();

            if (rows.IsSchemaWithoutTables())
            {
                tables = [];
            }
            else
            {
                tables =
                [
                    .. rows.GroupBy(d => d.Table!)
                        .Select(t => new Table(t.Key,
                        [
                            .. t.Select(r => new Column(
                                r.ColumnName ?? throw new InvalidOperationException("Missing column name."),
                                r.ColumnType ?? throw new InvalidOperationException("Missing column type."),
                                r.ColumnIsPrimaryKey,
                                r.ColumnIsNotNull,
                                r.ColumnIsAutoIncrement))
                        ]))
                ];
            }

            schemas.Add(new Schema(rowsBySchema.Key, tables));
        }

        return schemas;
    }

    private static bool IsSchemaWithoutTables(this SchemaInfo[] schemas)
        => schemas.Length == 1 && schemas.First().Table is null;
}
