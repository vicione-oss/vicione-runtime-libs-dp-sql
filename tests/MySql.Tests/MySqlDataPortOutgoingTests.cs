using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using MySqlConnector;
using NSubstitute;
using ViciOne.ManagedEngine.Runtime;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MySqlDataPortOutgoing_
{
    [Fact]
    public async Task DependencyInjectionProviderFactory_can_create_instance_Async()
    {
        await using var providerFactory = new ConstructorProviderFactory();

        var instance = providerFactory.CreateExternalOutgoing(
            new MySqlDataPortCommunication(),
            new(),
            Substitute.For<ILoggerFactory>(),
            Substitute.For<INameResolver>(),
            AssemblyLoadContext.Default);

        instance.Should().NotBeNull().And.BeOfType<MySqlDataPortOutgoing>();
    }
}

[Collection(MySqlTestEnvironment.Name)]
public class MySqlDataPortOutgoing_Interoperability(MySqlTestSystem mySql)
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

        var communication = mySql.Communication with { Nodes = tree, };
        using var outgoing = new MySqlDataPortOutgoing(communication, Substitute.For<ILogger<MySqlDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new MySqlConnection(communication.ToConnectionString());
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
        var communication = mySql.Communication with { Nodes = tree, };
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
        var communication = mySql.Communication with { Nodes = tree, };
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
        var communication = mySql.Communication with { Nodes = tree, };
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
        var communication = mySql.Communication with { Nodes = tree, };
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
