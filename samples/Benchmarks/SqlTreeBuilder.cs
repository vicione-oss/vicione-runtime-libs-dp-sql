using System;
using System.Collections.Generic;
using System.Linq;
using ViciOne.ManagedEngine.ExternalCommunication;
using ViciOne.Suite.DataPort;

namespace Benchmarks;

internal static class SqlTreeBuilder
{
    internal static List<List<ExternalValue>> CreateTestData(IReadOnlyCollection<INode> nodes, int count)
    {
        var columns = nodes.GetColumnChannels();
        List<List<ExternalValue>> data = [];

        for (var i = 0; i < count; i++)
        {
            data.Add([.. columns.Select(c => new ExternalValue
            {
                Channel = c,
                Value = Guid.NewGuid().ToString(),
            })]);
        }

        return data;
    }

    private static string[] GetColumnChannels(this IEnumerable<INode> nodes)
        => [.. nodes
            .Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId)
            .Select(n => n.AffectedChannels.Single())];

    internal static List<Node> CreateTree(ISqlThroughput sql)
        => CreateTree(sql.Schemas, sql.TablesPerSchema, sql.ColumnsPerTable);

    internal static List<Node> CreateTree(int schemas, int tablesPerSchema, int columnsPerTable)
    {
        List<Node> nodes = [];
        var databaseId = Guid.NewGuid();
        nodes.Add(CreateNode(databaseId, null, RelationalDatabaseNodeDesignId.DatabaseNodeId));

        for (var s = 0; s < schemas; s++)
        {
            var schemaId = Guid.NewGuid();
            nodes.Add(CreateNode(schemaId, databaseId, RelationalDatabaseNodeDesignId.SchemaNodeId));

            for (var t = 0; t < tablesPerSchema; t++)
            {
                var tableId = Guid.NewGuid();
                var tableNode = CreateNode(tableId, schemaId, RelationalDatabaseNodeDesignId.TableNodeId);
                nodes.Add(tableNode);

                for (var c = 0; c < columnsPerTable; c++)
                {
                    var channel = Guid.NewGuid().ToString();
                    nodes.Add(CreateValueNode(Guid.NewGuid(), channel, tableId, RelationalDatabaseNodeDesignId.ColumnNodeId, typeof(string)));
                    tableNode.TransferredChannels.Add(channel);
                }
            }
        }

        return nodes;
    }

    internal static List<Node> CreateTreeStartingWithTables(ISqlThroughput sql)
        => CreateTreeStartingWithTables(sql.TablesPerSchema, sql.ColumnsPerTable);

    internal static List<Node> CreateTreeStartingWithTables(int tablesPerSchema, int columnsPerTable)
    {
        List<Node> nodes = [];

        for (var t = 0; t < tablesPerSchema; t++)
        {
            var tableId = Guid.NewGuid();
            var tableNode = CreateNode(tableId, null, RelationalDatabaseNodeDesignId.TableNodeId);
            nodes.Add(tableNode);

            for (var c = 0; c < columnsPerTable; c++)
            {
                var channel = Guid.NewGuid().ToString();
                nodes.Add(CreateValueNode(Guid.NewGuid(), channel, tableId, RelationalDatabaseNodeDesignId.ColumnNodeId, typeof(string)));
                tableNode.TransferredChannels.Add(channel);
            }
        }

        return nodes;
    }

    private static Node CreateNode(Guid id, Guid? parent, string designId)
        => new()
        {
            Id = id,
            DesignId = designId,
            ParentId = parent,
            Name = ToDatabaseName(id),
        };

    private static Node CreateValueNode(Guid id, string channel, Guid? parent, string designId, Type type)
        => new()
        {
            Id = id,
            DesignId = designId,
            ParentId = parent,
            Name = ToDatabaseName(id),
            AffectedChannels = { channel, },
            ValueType = type,
        };

    private static string ToDatabaseName(Guid guid)
        => guid.ToString().Replace("-", string.Empty, StringComparison.InvariantCulture);
}
