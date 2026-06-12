using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace ViciOne.Suite.DataPort;

internal static class MySqlExtensions
{
    private static readonly Dictionary<Type, string> s_typeMapping = new()
    {
        { typeof(bool), "bool" },
        { typeof(short), "smallint"},
        { typeof(int), "int" },
        { typeof(long), "bigint" },
        { typeof(ushort), "smallint unsigned" },
        { typeof(uint), "int unsigned" },
        { typeof(ulong), "bigint unsigned" },
        { typeof(float), "float" },
        { typeof(double), "double" },
        { typeof(decimal), "decimal(65,30)" },
        { typeof(DateTime), "datetime" },
        { typeof(TimeSpan), "time" },
        { typeof(Guid), "varchar(36)"},
        { typeof(byte), "tinyint unsigned" },
        { typeof(byte[]), "varbinary(8000)" },
        { typeof(char), "text" },
        { typeof(string), "text" },
        { typeof(JsonNode), "json" },
    };

    internal static bool IsSupportedType(Type type)
        => s_typeMapping.ContainsKey(type);

    internal static string ConvertType(Type type)
        => s_typeMapping.TryGetValue(Nullable.GetUnderlyingType(type) ?? type, out var dbType) ? dbType : "Text";

    internal static IReadOnlyCollection<Schema> Unify(this IReadOnlyCollection<INode> nodes)
    {
        var schemaNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId);
        var tableNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.TableNodeId);
        var columnNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId);
        var nodeParentsCache = nodes.CreateNodeParentsCache();

        List<Schema> schemas = [];
        foreach (var schema in schemaNodes)
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
                        ConvertType(isJsonColumn ? typeof(JsonNode) : column.ValueType ?? throw new InvalidOperationException("The column must have a type.")),
                        primaryKey,
                        notNull,
                        autoIncrement));
                }

                tables.Add(new(table.Name, columns));
            }

            schemas.Add(new(schema.Name, tables));
        }

        return schemas;
    }

    internal static Dictionary<Guid, ColumnInfo> CreateColumnInfos(this IReadOnlyCollection<INode> nodes)
    {
        var schemaNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId);
        var tableNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.TableNodeId);
        var columnNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId);
        var nodeParentsCache = nodes.CreateNodeParentsCache();
        Dictionary<Guid, ColumnInfo> columns = [];

        foreach (var schema in schemaNodes)
        {
            foreach (var table in tableNodes.Where(sn => sn.ParentId == schema.Id))
            {
                var tableInfo = new TableInfo("default", schema.Name, table.Name);

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
