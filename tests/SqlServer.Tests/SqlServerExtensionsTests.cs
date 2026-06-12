using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqlServerExtensions_ConvertType
{
    [Theory]
    [InlineData(typeof(string), "Text")]
    [InlineData(typeof(int), "Int")]
    [InlineData(typeof(double), "Float")]
    [InlineData(typeof(decimal), "decimal(38,30)")]
    [MemberData(nameof(GetNullableTypes))]
    [SuppressMessage("Globalization", "CA1308:Zeichenfolgen in Großbuchstaben normalisieren", Justification = "<Ausstehend>")]
    public void Converts(Type type, string dbTypeName)
    {
        var result = SqlServerExtensions.ConvertType(type);

        result.Should().Be(dbTypeName.ToLower(CultureInfo.InvariantCulture));
    }

    public static TheoryData<Type, string> GetNullableTypes()
        => new()
        {
#pragma warning disable IDE0004 // Nicht erforderliche Umwandlung entfernen
            { ((bool?)false).GetType(), SqlDbType.Bit.ToString() },
#pragma warning restore IDE0004 // Nicht erforderliche Umwandlung entfernen
        };
}

public class SqlServerExtensions_Unify_Nodes
{
    [Fact]
    public void Acceptance()
    {
        Node databaseNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.DatabaseNodeId,
            Name = "MyDB",
        };
        Node schemaNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = databaseNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.SchemaNodeId,
            Name = "public",
        };
        Node usersTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = schemaNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "users",
        };
        Node idColumnUsersTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnUsersTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        Node ranksTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = schemaNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "ranks",
        };
        Node idColumnRanksTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnRanksTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        var nodes = new[] { databaseNode, schemaNode, usersTableNode, idColumnUsersTableNode, nameColumnUsersTableNode, ranksTableNode, idColumnRanksTableNode, nameColumnRanksTableNode, };

        var databases = SqlServerExtensions.Unify(nodes);

        var database = databases.Should().ContainSingle().Which;
        database.Name.Should().Be("MyDB");
        var schema = database.Schemas.Should().ContainSingle().Which;
        schema.Name.Should().Be("public");
        schema.Tables.Should().HaveCount(2).And.SatisfyRespectively(
            t =>
            {
                t.Name.Should().Be("users");
                t.Columns.Should().HaveCount(2).And.SatisfyRespectively(
                    c =>
                    {
                        c.Name.Should().Be("id");
                        c.Type.Should().Be("int");
                        c.PrimaryKey.Should().BeTrue();
                    },
                    c =>
                    {
                        c.Name.Should().Be("name");
                        c.Type.Should().Be("text");
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
                        c.Type.Should().Be("int");
                        c.PrimaryKey.Should().BeTrue();
                    },
                    c =>
                    {
                        c.Name.Should().Be("name");
                        c.Type.Should().Be("text");
                        c.PrimaryKey.Should().BeFalse();
                    });
            });
    }

    [Fact]
    public void Applies_default_type_to_json_column()
    {
        Node databaseNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.DatabaseNodeId,
            Name = "database",
        };
        Node schemaNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = databaseNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.SchemaNodeId,
            Name = "schema",
        };
        Node tableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = schemaNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "table",
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
        var nodes = new[] { databaseNode, schemaNode, tableNode, columnNode, jsonNode, };

        var databases = SqlServerExtensions.Unify(nodes);

        var column = databases.Should().ContainSingle().Which
            .Schemas.Should().ContainSingle().Which
            .Tables.Should().ContainSingle().Which
            .Columns.Should().ContainSingle().Which;
        column.Name.Should().Be("struct");
        column.Type.Should().Be("text");
    }

    [Theory]
    [InlineData(RelationalDatabaseNodeProperty.PrimaryKey)]
    [InlineData(RelationalDatabaseNodeProperty.AutoIncrement)]
    public void Froces_NotNull_to_true_when_PrimaryKey_or_AutoIncrement_is_true(string implicitNotNullProperty)
    {
        Node databaseNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.DatabaseNodeId,
            Name = "db",
        };
        Node schemaNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = databaseNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.SchemaNodeId,
            Name = "schema",
        };
        Node tableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = schemaNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "table",
        };
        Node columnNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = tableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { implicitNotNullProperty, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        var nodes = new[] { databaseNode, schemaNode, tableNode, columnNode, };

        var databases = SqlServerExtensions.Unify(nodes);

        var column = databases.Should().ContainSingle().Which
            .Schemas.Should().ContainSingle().Which
            .Tables.Should().ContainSingle().Which
            .Columns.Should().ContainSingle().Which;
        column.NotNull.Should().BeTrue();
    }
}

public class SqlServerExtensions_ToSchemas
{
    [Fact]
    public void Acceptance()
    {
        List<SchemaInfo> schemaInfos =
        [
            new("dbo", "users", "id", "int", true, false, false),
            new("dbo", "users", "name", "text", false, false, false),
            new("dbo", "ranks", "id", "int", true, false, false),
            new("dbo", "ranks", "name", "text", false, false, false),
            new("empty", null, null, null, false, false, false),
        ];

        var schemas = SqlServerExtensions.ToSchemas(schemaInfos);

        schemas.Should().HaveCount(2).And.SatisfyRespectively(
            s =>
            {
                s.Name.Should().Be("dbo");
                s.Tables.Should().HaveCount(2).And.SatisfyRespectively(
                    t =>
                    {
                        t.Name.Should().Be("users");
                        t.Columns.Should().HaveCount(2).And.SatisfyRespectively(
                            c =>
                            {
                                c.Name.Should().Be("id");
                                c.Type.Should().Be("int");
                                c.PrimaryKey.Should().BeTrue();
                            },
                            c =>
                            {
                                c.Name.Should().Be("name");
                                c.Type.Should().Be("text");
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
                                c.Type.Should().Be("int");
                                c.PrimaryKey.Should().BeTrue();
                            },
                            c =>
                            {
                                c.Name.Should().Be("name");
                                c.Type.Should().Be("text");
                                c.PrimaryKey.Should().BeFalse();
                            });
                    });
            },
            s =>
            {
                s.Name.Should().Be("empty");
                s.Tables.Should().BeEmpty();
            });
    }
}

public class SqlServerExtensions_CreateColumnInfos
{
    [Fact]
    public void Acceptance()
    {
        Node databaseNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.DatabaseNodeId,
            Name = "MyDB",
        };
        Node schemaNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = databaseNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.SchemaNodeId,
            Name = "public",
        };
        Node usersTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = schemaNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "users",
        };
        Node idColumnUsersTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnUsersTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = usersTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        Node ranksTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = schemaNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.TableNodeId,
            Name = "ranks",
        };
        Node idColumnRanksTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Properties = new() { { RelationalDatabaseNodeProperty.PrimaryKey, new() { Value = true, } } },
            Name = "id",
            ValueType = typeof(int),
        };
        Node nameColumnRanksTableNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = ranksTableNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
        };
        var nodes = new[] { databaseNode, schemaNode, usersTableNode, idColumnUsersTableNode, nameColumnUsersTableNode, ranksTableNode, idColumnRanksTableNode, nameColumnRanksTableNode, };

        var columnInfos = SqlServerExtensions.CreateColumnInfos(nodes);

        columnInfos.Should().HaveCount(4).And.SatisfyRespectively(
            ci =>
            {
                ci.Key.Should().Be(idColumnUsersTableNode.Id.ToString());
                ci.Value.Name.Should().Be("id");
                ci.Value.TableInfo.Database.Should().Be("MyDB");
                ci.Value.TableInfo.Schema.Should().Be("public");
                ci.Value.TableInfo.Table.Should().Be("users");
            },
            ci =>
            {
                ci.Key.Should().Be(nameColumnUsersTableNode.Id.ToString());
                ci.Value.Name.Should().Be("name");
                ci.Value.TableInfo.Database.Should().Be("MyDB");
                ci.Value.TableInfo.Schema.Should().Be("public");
                ci.Value.TableInfo.Table.Should().Be("users");
            },
            ci =>
            {
                ci.Key.Should().Be(idColumnRanksTableNode.Id.ToString());
                ci.Value.Name.Should().Be("id");
                ci.Value.TableInfo.Database.Should().Be("MyDB");
                ci.Value.TableInfo.Schema.Should().Be("public");
                ci.Value.TableInfo.Table.Should().Be("ranks");
            },
            ci =>
            {
                ci.Key.Should().Be(nameColumnRanksTableNode.Id.ToString());
                ci.Value.Name.Should().Be("name");
                ci.Value.TableInfo.Database.Should().Be("MyDB");
                ci.Value.TableInfo.Schema.Should().Be("public");
                ci.Value.TableInfo.Table.Should().Be("ranks");
            });
    }
}
