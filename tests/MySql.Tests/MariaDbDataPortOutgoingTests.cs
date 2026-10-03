using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using MySqlConnector;
using NSubstitute;
using Xunit;

namespace ViciOne.Suite.DataPort;

[Collection(MariaDbTestEnvironment.Name)]
public class MariaDbDataPortOutgoing_Interoperability(MariaDbTestSystem mariaDb)
{
    [Fact]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    public async Task Can_create_structure()
    {
        var tree = new SqlTreeCreator()
            .AddDb("StructureDB")
            .AddSchema("StructureSchema")
            .AddTable("string")
            .AddColumn("value", typeof(string))
            .AddTable("TimeSpan")
            .AddColumn("value", typeof(TimeSpan))
            .AddTable("DateTime")
            .AddColumn("value", typeof(DateTime))
            .AddTable("double")
            .AddColumn("value", typeof(double))
            .AddTable("deciaml")
            .AddColumn("value", typeof(decimal))
            .Create();
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new MySqlConnection(mariaDb.Communication.ToConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var command = new MySqlCommand(ComposeSelectSchemaCommand("StructureSchema"), connection);
        var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var result = new List<SchemaInfo>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var datatype = await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(3, TestContext.Current.CancellationToken);

            result.Add(new(
                await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(2, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : GetDataTypePrecision(reader, datatype),
                await reader.GetFieldValueAsync<bool>(4, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(5, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(6, TestContext.Current.CancellationToken)));
        }

        result.Should()
            .AllSatisfy(e => e.Schema.Should().Be("StructureSchema"))
            .And.HaveCount(5)
            .And.Contain(e => e.Table == "deciaml")
            .And.Contain(e => e.Table == "DateTime")
            .And.Contain(e => e.Table == "double")
            .And.Contain(e => e.Table == "TimeSpan")
            .And.Contain(e => e.Table == "string");
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    public async Task Can_create_columns_with_AutoIncrement_and_NotNull_or_PrimaryKey()
    {
        var tree = new SqlTreeCreator()
            .AddDb("StructureDB")
            .AddSchema("ConstrainsSchema")
            .AddTable("PrimaryKey")
            .AddColumn("value", typeof(int), primaryKey: true, autoIncrement: true)
            .AddTable("NotNull")
            .AddColumn("value", typeof(int), notNull: true)
            .Create();
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new MySqlConnection(communication.ToConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using var command = new MySqlCommand(ComposeSelectSchemaCommand("ConstrainsSchema"), connection);
        var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var result = new List<SchemaInfo>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var datatype = await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(3, TestContext.Current.CancellationToken);

            result.Add(new(
                await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(2, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : GetDataTypePrecision(reader, datatype),
                await reader.GetFieldValueAsync<bool>(4, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(5, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(6, TestContext.Current.CancellationToken)));
        }

        result.Should()
            .AllSatisfy(e => e.Schema.Should().Be("ConstrainsSchema"))
            .And.HaveCount(2)
            .And.Contain(e => e.ColumnIsPrimaryKey)
            .And.Contain(e => e.ColumnIsNotNull && !e.ColumnIsPrimaryKey && !e.ColumnIsAutoIncrement)
            .And.Contain(e => e.ColumnIsAutoIncrement);
    }

    private static string? GetDataTypePrecision(DbDataReader reader, string? dataType)
        => dataType switch
        {
            "decimal" => $"{dataType}({reader.GetFieldValue<byte>(7)},{reader.GetFieldValue<byte>(8)})",
            "varbinary" => $"{dataType}(8000)",
            _ => dataType,
        };

    [Theory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
    public async Task Can_handle_different_data_types(Type type, object value)
    {
        var tree = new SqlTreeCreator()
            .AddDb("DataTypTestDB")
            .AddSchema("DataTypTestSchema")
            .AddTable(type.Name)
            .AddColumn("value", type)
            .Create();
        var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, type);
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new MySqlConnection(mariaDb.Communication.ToConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('`',
            tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name,
            tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId).Name);

        using var command = new MySqlCommand(query, connection);

        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().BeEquivalentTo(value);
        await connection.CloseAsync();
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
    [SuppressMessage("Usage", "CA2201:Keine reservierten Ausnahmetypen auslösen")]
    public async Task Logs_PublishFailed_on_sending_null_to_not_null()
    {
        var tree = new SqlTreeCreator()
            .AddDb("DataTypTestDB")
            .AddSchema("DataTypTestSchema")
            .AddTable("NotNull")
            .AddColumn("value", typeof(int), notNull: true)
            .Create();
        int? value = null;
        var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, typeof(int));
        var communication = mariaDb.Communication with { Nodes = tree, };
        FakeLogger<MySqlDataPortOutgoing> logger = new();
        using var outgoing = new MySqlDataPortOutgoing(communication, logger);

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        logger.LatestRecord.Message.Should().Contain("Cannot");
        logger.LatestRecord.Id.Should().Be(new EventId(1, "LogPublishWithCompletedQueueFailed"));
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
    public async Task Can_handle_auto_increment()
    {
        var tree = new SqlTreeCreator()
            .AddDb("DataTypTestDB")
            .AddSchema("DataTypTestSchema")
            .AddTable("autoIncrement")
            .AddColumn("id", typeof(int), autoIncrement: true, primaryKey: true)
            .AddColumn("value", typeof(string))
            .Create();
        var data = RelationalDatabaseTestHelper.CreateTestData(tree, "test", typeof(string));
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new MySqlConnection(communication.ToConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('`',
            tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name,
            tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId).Name);
        using var command = new MySqlCommand(query, connection);

        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().BeEquivalentTo(1);
        await connection.CloseAsync();
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    public async Task Can_reconnect_to_a_table_with_a_string_PrimaryKey()
    {
        var tree = new SqlTreeCreator()
            .AddDb("ReconnectDB")
            .AddSchema("ReconnectSchema")
            .AddTable("machines")
            .AddColumn("id", typeof(string), primaryKey: true)
            .Create();
        var data = RelationalDatabaseTestHelper.CreateTestData(tree, "machine-1", typeof(string));
        var communication = mariaDb.Communication with { Nodes = tree, };
        using (var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>()))
        {
            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);
        }
        using var reconnected = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        var act = () => reconnected.ConnectAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    public async Task Writes_again_after_the_server_closed_the_connection_Async()
    {
        var tree = new SqlTreeCreator()
            .AddDb("ReconnectDB")
            .AddSchema("KilledConnectionSchema")
            .AddTable("measurements")
            .AddColumn("value", typeof(int))
            .Create();
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());
        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, 1, typeof(int)), TestContext.Current.CancellationToken);
        await MySqlTestQueries.WaitForRowsAsync(communication, "KilledConnectionSchema", "measurements", 1, TestContext.Current.CancellationToken);
        await MySqlTestQueries.KillOtherConnectionsAsync(communication, TestContext.Current.CancellationToken);

        await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, 2, typeof(int)), TestContext.Current.CancellationToken);

        var rows = await MySqlTestQueries.WaitForRowsAsync(communication, "KilledConnectionSchema", "measurements", 2, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);
        rows.Select(r => r[0]).Should().Equal(1, 2);
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    public async Task Writes_rows_with_different_columns_in_one_batch_Async()
    {
        var tree = new SqlTreeCreator()
            .AddDb("MixedDB")
            .AddSchema("MixedSchema")
            .AddTable("mixed")
            .AddColumn("a", typeof(int))
            .AddColumn("b", typeof(string))
            .Create();
        var onlyA = RelationalDatabaseTestHelper.CreateTestData(tree, 1, typeof(int));
        var onlyB = RelationalDatabaseTestHelper.CreateTestData(tree, "x", typeof(string));
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());
        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);

        await outgoing.SendAsync(0, [.. onlyA, .. onlyB], TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, onlyA, TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, onlyB, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        var rows = await MySqlTestQueries.SelectAllAsync(communication, "MixedSchema", "mixed", TestContext.Current.CancellationToken);
        rows.Should().BeEquivalentTo<object?[]>([[1, "x"], [1, null], [null, "x"]], o => o.WithStrictOrdering());
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    public async Task Creates_tables_in_an_existing_database_without_tables_Async()
    {
        var tree = new SqlTreeCreator()
            .AddDb("ExistingDB")
            .AddSchema("EmptyMariaDbDatabase")
            .AddTable("measurements")
            .AddColumn("value", typeof(int))
            .Create();
        var communication = mariaDb.Communication with { Nodes = tree, };
        await MySqlTestQueries.ExecuteAsync(communication, "CREATE DATABASE `EmptyMariaDbDatabase`;", TestContext.Current.CancellationToken);
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        var rows = await MySqlTestQueries.SelectAllAsync(communication, "EmptyMariaDbDatabase", "measurements", TestContext.Current.CancellationToken);
        rows.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    public async Task Connects_again_to_the_tables_it_created_Async()
    {
        var tree = new SqlTreeCreator()
            .AddDb("RedeployDB")
            .AddSchema("RedeploySchema")
            .AddTable("all_types")
            .AddColumn("bool", typeof(bool))
            .AddColumn("u8", typeof(byte))
            .AddColumn("i8", typeof(sbyte))
            .AddColumn("i16", typeof(short))
            .AddColumn("u16", typeof(ushort))
            .AddColumn("i32", typeof(int))
            .AddColumn("u32", typeof(uint))
            .AddColumn("i64", typeof(long))
            .AddColumn("u64", typeof(ulong))
            .AddColumn("f32", typeof(float))
            .AddColumn("f64", typeof(double))
            .AddColumn("dec", typeof(decimal))
            .AddColumn("str", typeof(string))
            .AddColumn("dt", typeof(DateTime))
            .AddColumn("ts", typeof(TimeSpan))
            .AddColumn("bin", typeof(byte[]))
            .AddTable("keyed")
            .AddColumn("id", typeof(string), primaryKey: true)
            .Create();
        var communication = mariaDb.Communication with { Nodes = tree, };
        using (var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>()))
        {
            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);
        }
        using var redeployed = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        var act = () => redeployed.ConnectAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        await redeployed.DisconnectAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    public async Task Keeps_the_microseconds_of_a_DateTime_Async()
    {
        var value = new DateTime(2026, 10, 3, 6, 0, 0, DateTimeKind.Utc).AddTicks(1_234_560);
        var tree = new SqlTreeCreator()
            .AddDb("DateTimeDB")
            .AddSchema("DateTimeSchema")
            .AddTable("times")
            .AddColumn("value", typeof(DateTime))
            .Create();
        var communication = mariaDb.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());
        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);

        await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, value, typeof(DateTime)), TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        var rows = await MySqlTestQueries.SelectAllAsync(communication, "DateTimeSchema", "times", TestContext.Current.CancellationToken);
        rows.Should().ContainSingle().Which[0].Should().BeOfType<DateTime>().Which.Ticks.Should().Be(value.Ticks);
    }

    public static TheoryData<Type, object> GetData()
        => new()
        {
            { typeof(string), "test" },
            { typeof(bool), true },
            { typeof(float), 0.5f },
            { typeof(double), 1.5d },
            { typeof(decimal), 1.55 },
            { typeof(DateTime), new DateTime(2024, 10, 23) },
            { typeof(TimeSpan), TimeSpan.FromMinutes(5.5) },
            { typeof(char), "A" },
            { typeof(byte[]), new byte[] { 1,1,1,1 } },
            { typeof(byte), (byte)2 },
            { typeof(sbyte), (sbyte)-3 },
            { typeof(ushort), (ushort)7 },
            { typeof(uint), (uint)7 },
            { typeof(int), 1 },
            { typeof(long), 1000000L },
            { typeof(short), (short)2 },
            { typeof(Guid), Guid.Empty.ToString() },
        };

    private static string ComposeSelectSchemaCommand(string schema)
        => $"""
            SELECT
                TABLE_SCHEMA AS "schema",
                TABLE_NAME AS "table",
                COLUMN_NAME AS "column",
                DATA_TYPE AS "type",
                CASE WHEN COLUMN_KEY = 'PRI' THEN true ELSE false END AS "primary_key",
                CASE WHEN IS_NULLABLE = 'NO' THEN true ELSE false END AS "not_null",
                CASE WHEN EXTRA LIKE '%auto_increment%' THEN true ELSE false END AS "auto_increment",
                NUMERIC_PRECISION as "precision",
                NUMERIC_SCALE as "scale"
            FROM information_schema.columns
            WHERE TABLE_SCHEMA = '{schema}'
            """;
}
