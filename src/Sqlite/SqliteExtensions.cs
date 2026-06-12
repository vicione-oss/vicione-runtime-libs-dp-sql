using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace ViciOne.Suite.DataPort;

internal static class SqliteExtensions
{
    private static readonly Dictionary<Type, SqliteType> s_typeMapping = new()
    {
        { typeof(bool), SqliteType.Integer },
        { typeof(short), SqliteType.Integer },
        { typeof(int), SqliteType.Integer },
        { typeof(long), SqliteType.Integer },
        { typeof(ushort), SqliteType.Integer },
        { typeof(uint), SqliteType.Integer },
        { typeof(ulong), SqliteType.Integer }, // DB does not support this type, stack overflow possible
        { typeof(float), SqliteType.Real },
        { typeof(double), SqliteType.Real },
        { typeof(decimal), SqliteType.Text },
        { typeof(DateTime), SqliteType.Text },
        { typeof(TimeSpan), SqliteType.Text },
        { typeof(Guid), SqliteType.Text },
        { typeof(byte), SqliteType.Integer },
        { typeof(byte[]), SqliteType.Blob },
        { typeof(char), SqliteType.Text },
        { typeof(string), SqliteType.Text },
    };

    internal static bool IsSupportedType(Type type)
        => s_typeMapping.ContainsKey(type);

    internal static string ConvertType(Type type)
        => (s_typeMapping.TryGetValue(Nullable.GetUnderlyingType(type) ?? type, out var dbType) ? dbType : SqliteType.Text)
            .ToString().ToUpperInvariant();

    internal static Database Unify(this IReadOnlyCollection<INode> nodes)
    {
        var tableNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).ToDictionary(n => n.Id);
        var columnNodes = nodes.Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId);
        var tableColumns = columnNodes.Where(n => n.ParentId is not null).GroupBy(n => n.ParentId);
        var nodeParentsCache = nodes.CreateNodeParentsCache();

        var tables = tableColumns
            .Select(c => new Table(
                tableNodes[c.Key!.Value].Name,
                [
                    .. c.Select(cc =>
                    {
                        var isJsonColumn = cc.IsJsonColumn(nodeParentsCache);
                        return new Column(
                            cc.Name,
                            ConvertType(isJsonColumn ? typeof(string) : cc.ValueType ?? throw new InvalidOperationException("The column must have a type.")),
                            cc.GetPropertyValueOrDefault(RelationalDatabaseNodeProperty.PrimaryKey, false),
                            cc.GetPropertyValueOrDefault(RelationalDatabaseNodeProperty.NotNull, false),
                            false);
                    })
                ])).ToList();

        return new Database("default", [new("default", tables),]);
    }

    internal static Dictionary<Guid, ColumnInfo> CreateColumnInfos(this IReadOnlyCollection<INode> nodes)
    {
        var tableInfos = nodes
            .Where(n => n.DesignId == RelationalDatabaseNodeDesignId.TableNodeId)
            .ToDictionary(n => n.Id, n => new TableInfo("default", "default", n.Name));
        var nodeParentsCache = nodes.CreateNodeParentsCache();

        return nodes
            .Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId)
            .Where(n => n.ParentId is not null)
            .GroupBy(n => n.ParentId)
            .SelectMany(g =>
            {
                var tableInfo = tableInfos[g.Key!.Value];
                return g.Select(n => (n.Id, CreateColumnInfo(tableInfo, n)));
            })
            .ToDictionary(e => e.Id, e => e.Item2);

        ColumnInfo CreateColumnInfo(TableInfo tableInfo, INode node)
            => new(
                tableInfo,
                node.Name,
                node.IsJsonColumn(nodeParentsCache),
                node.ValueType is not null && !IsSupportedType(node.ValueType),
                node.ValueType);
    }

    internal static Database Unify(this IReadOnlyCollection<TablesWithColumnsResult> data)
    {
        var tables = data
            .GroupBy(r => r.Table)
            .Select(t => new Table(t.Key, [.. t.Select(r => new Column(r.ColumnName, r.ColumnType, r.ColumnIsPrimaryKey, r.ColumnIsNotNull, false))]))
            .ToList();
        return new Database("default", [new("default", tables),]);
    }
}
