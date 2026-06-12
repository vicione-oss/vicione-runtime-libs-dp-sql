using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ViciOne.Suite.DataPort;

[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public sealed class SqlTreeCreator
{
    private readonly List<Node> _nodes = [];
    private Guid _currentDatabase = Guid.Empty;
    private Guid _currentSchema = Guid.Empty;
    private Node? _currentTable;

    public SqlTreeCreator AddDb(string name)
    {
        _currentDatabase = Guid.NewGuid();
        _nodes.Add(CreateNode(name, _currentDatabase, null, RelationalDatabaseNodeDesignId.DatabaseNodeId));

        return this;
    }

    public SqlTreeCreator AddSchema(string name)
    {
        _currentSchema = Guid.NewGuid();
        _nodes.Add(CreateNode(name, _currentSchema, _currentDatabase, RelationalDatabaseNodeDesignId.SchemaNodeId));

        return this;
    }

    public SqlTreeCreator AddTable(string name)
    {
        _currentTable = CreateNode(name, Guid.NewGuid(), _currentSchema, RelationalDatabaseNodeDesignId.TableNodeId);
        _nodes.Add(_currentTable);

        return this;
    }

    public SqlTreeCreator AddColumn(string name, Type type, bool notNull = false, bool autoIncrement = false, bool primaryKey = false)
    {
        var channel = Guid.NewGuid().ToString();
        var node = CreateValueNode(name, Guid.NewGuid(), channel, _currentTable?.Id, RelationalDatabaseNodeDesignId.ColumnNodeId, type);

        node.Properties.Add(RelationalDatabaseNodeProperty.NotNull, new() { Value = notNull, Channels = [channel] });
        node.Properties.Add(RelationalDatabaseNodeProperty.AutoIncrement, new() { Value = autoIncrement, Channels = [channel] });
        node.Properties.Add(RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = primaryKey, Channels = [channel] });
        _nodes.Add(node);
        _currentTable?.TransferredChannels.Add(channel);

        return this;
    }

    public IReadOnlyCollection<Node> Create() => _nodes;

    private static Node CreateNode(string name, Guid id, Guid? parent, string designId)
        => new()
        {
            Id = id,
            DesignId = designId,
            ParentId = parent,
            Name = ToDatabaseName(name),
        };

    private static Node CreateValueNode(string name, Guid id, string channel, Guid? parent, string designId, Type type)
        => new()
        {
            Id = id,
            DesignId = designId,
            ParentId = parent,
            Name = ToDatabaseName(name),
            AffectedChannels = { channel, },
            ValueType = type,
        };

    private static string ToDatabaseName(string name)
        => name.ToString().Replace("-", string.Empty, StringComparison.InvariantCulture);
}
