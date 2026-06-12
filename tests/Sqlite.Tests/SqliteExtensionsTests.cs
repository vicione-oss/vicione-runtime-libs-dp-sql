using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteExtensions_Unify_Nodes
{
    [Fact]
    public void Acceptance()
    {
        Node usersTable = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "users",
        };
        Node idColumnUsersTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnUsersTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        Node ranksTable = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "ranks",
        };
        Node idColumnRanksTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnRanksTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        var nodes = new[] { usersTable, idColumnUsersTable, nameColumnUsersTable, ranksTable, idColumnRanksTable, nameColumnRanksTable, };

        var database = SqliteExtensions.Unify(nodes);

        var schema = database.Schemas.Should().ContainSingle().Which;
        schema.Name.Should().Be("default");
        schema.Tables.Should().HaveCount(2).And.SatisfyRespectively(
            t =>
            {
                t.Name.Should().Be("users");
                t.Columns.Should().HaveCount(2).And.SatisfyRespectively(
                    c =>
                    {
                        c.Name.Should().Be("id");
                        c.Type.Should().Be("INTEGER");
                        c.PrimaryKey.Should().BeTrue();
                    },
                    c =>
                    {
                        c.Name.Should().Be("name");
                        c.Type.Should().Be("TEXT");
                        c.PrimaryKey.Should().BeFalse();
                    });
            },
            t =>
            {
                t.Name.Should().Be("ranks");
                t.Columns.Should().HaveCount(2).And.SatisfyRespectively(
                    c =>
                    {
                        c.Name.Should().Be("id");
                        c.Type.Should().Be("INTEGER");
                        c.PrimaryKey.Should().BeTrue();
                    },
                    c =>
                    {
                        c.Name.Should().Be("name");
                        c.Type.Should().Be("TEXT");
                        c.PrimaryKey.Should().BeFalse();
                    });
            });
    }

    [Fact]
    public void Applies_default_type_to_json_column()
    {
        Node tableNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "users",
        };
        Node columnNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = tableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "struct",
        };
        Node jsonNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = columnNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.JsonStructureNodeId,
            Name = "value",
        };
        var nodes = new[] { tableNode, columnNode, jsonNode, };

        var database = SqliteExtensions.Unify(nodes);

        var schema = database.Schemas.Should().ContainSingle().Which;
        var table = schema.Tables.Should().ContainSingle().Which;
        var column = table.Columns.Should().ContainSingle().Which;
        column.Name.Should().Be("struct");
        column.Type.Should().Be("TEXT");
    }
}

public class SqliteExtensions_Unify_Datatable
{
    [Fact]
    public void Acceptance()
    {
        List<TablesWithColumnsResult> data =
        [
            new("users", "id", "INTEGER", true, false),
            new("users", "name", "TEXT", false, false),
            new("ranks", "id", "INTEGER", true, false),
            new("ranks", "name", "TEXT", false, false),
        ];

        var database = SqliteExtensions.Unify(data);

        var schema = database.Schemas.Should().ContainSingle().Which;
        schema.Name.Should().Be("default");
        schema.Tables.Should().HaveCount(2).And.SatisfyRespectively(
            t =>
            {
                t.Name.Should().Be("users");
                t.Columns.Should().HaveCount(2).And.SatisfyRespectively(
                    c =>
                    {
                        c.Name.Should().Be("id");
                        c.Type.Should().Be("INTEGER");
                        c.PrimaryKey.Should().BeTrue();
                    },
                    c =>
                    {
                        c.Name.Should().Be("name");
                        c.Type.Should().Be("TEXT");
                        c.PrimaryKey.Should().BeFalse();
                    });
            },
            t =>
            {
                t.Name.Should().Be("ranks");
                t.Columns.Should().HaveCount(2).And.SatisfyRespectively(
                    c =>
                    {
                        c.Name.Should().Be("id");
                        c.Type.Should().Be("INTEGER");
                        c.PrimaryKey.Should().BeTrue();
                    },
                    c =>
                    {
                        c.Name.Should().Be("name");
                        c.Type.Should().Be("TEXT");
                        c.PrimaryKey.Should().BeFalse();
                    });
            });
    }
}

public class ConvertType
{
    [Theory]
    [InlineData(typeof(string), SqliteType.Text)]
    [InlineData(typeof(int), SqliteType.Integer)]
    [InlineData(typeof(double), SqliteType.Real)]
    [InlineData(typeof(byte[]), SqliteType.Blob)]
    [MemberData(nameof(GetNullableTypes))]
    public void Converts(Type type, SqliteType dbType)
    {
        var result = SqliteExtensions.ConvertType(type);

        result.Should().Be(dbType.ToString().ToUpperInvariant());
    }

    public static TheoryData<Type, SqliteType> GetNullableTypes()
        => new()
        {
#pragma warning disable IDE0004 // Nicht erforderliche Umwandlung entfernen
            { ((bool?)false).GetType(), SqliteType.Integer },
#pragma warning restore IDE0004 // Nicht erforderliche Umwandlung entfernen
        };
}

public class SqliteExtensions_CreateColumnInfos
{
    [Fact]
    public void Acceptance()
    {
        Node usersTable = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "users",
        };
        Node idColumnUsersTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnUsersTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        Node ranksTable = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "ranks",
        };
        Node idColumnRanksTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnRanksTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        Node metaColumnRankTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "meta",
        };
        Node metaValueRanksTable = new()
        {
            Id = Guid.NewGuid(),
            ParentId = metaColumnRankTable.Id,
            DesignId = RelationalDatabaseNodeDesignId.JsonStructureNodeId,
            Name = "value",
            ValueType = typeof(int),
        };
        var nodes = new[] { usersTable, idColumnUsersTable, nameColumnUsersTable, ranksTable, idColumnRanksTable, nameColumnRanksTable, metaColumnRankTable, metaValueRanksTable, };

        var columnInfos = SqliteExtensions.CreateColumnInfos(nodes);

        columnInfos.Should().HaveCount(5).And.SatisfyRespectively(
            ci =>
            {
                ci.Key.Should().Be(idColumnUsersTable.Id.ToString());
                ci.Value.Name.Should().Be("id");
                ci.Value.IsJsonStructure.Should().BeFalse();
                ci.Value.TableInfo.Database.Should().Be("default");
                ci.Value.TableInfo.Schema.Should().Be("default");
                ci.Value.TableInfo.Table.Should().Be("users");
            },
            ci =>
            {
                ci.Key.Should().Be(nameColumnUsersTable.Id.ToString());
                ci.Value.Name.Should().Be("name");
                ci.Value.IsJsonStructure.Should().BeFalse();
                ci.Value.TableInfo.Database.Should().Be("default");
                ci.Value.TableInfo.Schema.Should().Be("default");
                ci.Value.TableInfo.Table.Should().Be("users");
            },
            ci =>
            {
                ci.Key.Should().Be(idColumnRanksTable.Id.ToString());
                ci.Value.Name.Should().Be("id");
                ci.Value.IsJsonStructure.Should().BeFalse();
                ci.Value.TableInfo.Database.Should().Be("default");
                ci.Value.TableInfo.Schema.Should().Be("default");
                ci.Value.TableInfo.Table.Should().Be("ranks");
            },
            ci =>
            {
                ci.Key.Should().Be(nameColumnRanksTable.Id.ToString());
                ci.Value.Name.Should().Be("name");
                ci.Value.IsJsonStructure.Should().BeFalse();
                ci.Value.TableInfo.Database.Should().Be("default");
                ci.Value.TableInfo.Schema.Should().Be("default");
                ci.Value.TableInfo.Table.Should().Be("ranks");
            },
            ci =>
            {
                ci.Key.Should().Be(metaColumnRankTable.Id.ToString());
                ci.Value.Name.Should().Be("meta");
                ci.Value.IsJsonStructure.Should().BeTrue();
                ci.Value.TableInfo.Database.Should().Be("default");
                ci.Value.TableInfo.Schema.Should().Be("default");
                ci.Value.TableInfo.Table.Should().Be("ranks");
            });
    }
}
