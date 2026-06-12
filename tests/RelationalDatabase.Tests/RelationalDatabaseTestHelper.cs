using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AwesomeAssertions;
using ViciOne.ManagedEngine.ExternalCommunication;

namespace ViciOne.Suite.DataPort;


[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public static class RelationalDatabaseTestHelper
{
    public static List<ExternalValue> CreateTestData(IReadOnlyCollection<Node> nodes, object? value, Type type)
        => [.. GetColumnChannels(nodes.Where(n => n.ValueType == type)).Select(c => new ExternalValue
        {
            Channel = c,
            Value = value,
        })];

    private static string[] GetColumnChannels(IEnumerable<Node> nodes)
        => [.. nodes
            .Where(n => n.DesignId == RelationalDatabaseNodeDesignId.ColumnNodeId)
            .Select(n => n.AffectedChannels.Single())];

    public static string ComposeSelectTableQuery(char escapeChar, string tableName, string? schema = default)
    {
        if (schema is not null)
            schema = $"{escapeChar}{schema}{escapeChar}.";
        return $"SELECT * FROM {schema}{escapeChar}{tableName}{escapeChar};";
    }
}
