using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using ViciOne.ManagedEngine.Runtime;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteDataPortOutgoing_
{
    [Fact]
    public async Task DependencyInjectionProviderFactory_can_create_instance_Async()
    {
        await using var providerFactory = new ConstructorProviderFactory();

        var instance = providerFactory.CreateExternalOutgoing(
            new SqliteDataPortCommunication(),
            new(),
            Substitute.For<ILoggerFactory>(),
            Substitute.For<INameResolver>(),
            AssemblyLoadContext.Default);

        instance.Should().NotBeNull().And.BeOfType<SqliteDataPortOutgoing>();
    }

    private const string SelectAllSchemasWithInformation =
        "SELECT tables.name, columns.name, columns.type, columns.pk, columns.\"notnull\" " +
        "FROM sqlite_master AS tables, pragma_table_info(tables.name) AS columns " +
        "WHERE tables.type ='table' AND tables.name NOT LIKE 'sqlite_%'";

    [Fact]
    public async Task Can_create_structure()
    {
        var filename = $"{nameof(Can_create_structure)}.db";

        File.Delete(filename);

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
        using var outgoing = new SqliteDataPortOutgoing(new() { Filename = filename, Nodes = tree }, Substitute.For<ILogger<SqliteDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = filename,
            Pooling = false,
        };

        using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using var command = new SqliteCommand(SelectAllSchemasWithInformation, connection);
        var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var result = new List<TablesWithColumnsResult>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var datatype = await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(3, TestContext.Current.CancellationToken);

            result.Add(new(
                await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<string>(2, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(3, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(4, TestContext.Current.CancellationToken)));
        }

        result.Should()
            .HaveCount(5)
            .And.Contain(e => e.Table == "deciaml")
            .And.Contain(e => e.Table == "DateTime")
            .And.Contain(e => e.Table == "double")
            .And.Contain(e => e.Table == "TimeSpan")
            .And.Contain(e => e.Table == "string");
    }

    [Fact]
    public async Task Can_create_columns_with_NotNull_or_PrimaryKey()
    {
        var filename = $"{nameof(Can_create_columns_with_NotNull_or_PrimaryKey)}.db";

        File.Delete(filename);

        var tree = new SqlTreeCreator()
            .AddDb("StructureDB")
            .AddSchema("ConstrainsSchema")
            .AddTable("PrimaryKey")
            .AddColumn("value", typeof(int), primaryKey: true)
            .AddTable("NotNull")
            .AddColumn("value", typeof(int), notNull: true)
            .Create();
        using var outgoing = new SqliteDataPortOutgoing(new() { Filename = filename, Nodes = tree }, Substitute.For<ILogger<SqliteDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = filename,
            Pooling = false,
        };

        using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using var command = new SqliteCommand(SelectAllSchemasWithInformation, connection);
        var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var result = new List<TablesWithColumnsResult>();

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var datatype = await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(3, TestContext.Current.CancellationToken);

            result.Add(new(
                await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<string>(2, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(3, TestContext.Current.CancellationToken),
                await reader.GetFieldValueAsync<bool>(4, TestContext.Current.CancellationToken)));
        }

        result.Should()
            .HaveCount(2)
            .And.Contain(e => e.ColumnIsPrimaryKey)
            .And.Contain(e => e.ColumnIsNotNull && !e.ColumnIsPrimaryKey);
    }

    [Theory]
    [MemberData(nameof(GetData))]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
    public async Task Can_handle_different_data_types(Type type, object value, object expectedValue)
    {
        var filename = $"{nameof(Can_handle_different_data_types)}_{type.Name}.db";

        File.Delete(filename);

        var tree = new SqlTreeCreator()
            .AddDb("DataTypTestDB")
            .AddSchema("DataTypTestSchema")
            .AddTable(type.Name)
            .AddColumn("value", type)
            .Create();

        var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, type);
        using var outgoing = new SqliteDataPortOutgoing(new() { Filename = filename, Nodes = tree }, Substitute.For<ILogger<SqliteDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = filename,
            Pooling = false,
        };

        using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('"',
            tree.First(e => e.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name);
        using var command = new SqliteCommand(query, connection);

        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().BeEquivalentTo(expectedValue);
        await connection.CloseAsync();
    }

    [Fact]
    public async Task Writes_rows_with_different_columns_in_one_batch_Async()
    {
        var filename = $"{nameof(Writes_rows_with_different_columns_in_one_batch_Async)}.db";
        File.Delete(filename);
        var tree = new SqlTreeCreator()
            .AddDb("MixedDB")
            .AddSchema("MixedSchema")
            .AddTable("mixed")
            .AddColumn("a", typeof(int))
            .AddColumn("b", typeof(string))
            .Create();
        var onlyA = RelationalDatabaseTestHelper.CreateTestData(tree, 1, typeof(int));
        var onlyB = RelationalDatabaseTestHelper.CreateTestData(tree, "x", typeof(string));
        using var outgoing = new SqliteDataPortOutgoing(new() { Filename = filename, Nodes = tree }, Substitute.For<ILogger<SqliteDataPortOutgoing>>());
        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);

        await outgoing.SendAsync(0, [.. onlyA, .. onlyB], TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, onlyA, TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, onlyB, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        var rows = await SqliteTestQueries.SelectAllAsync(filename, "mixed", TestContext.Current.CancellationToken);
        rows.Should().BeEquivalentTo<object?[]>([[1L, "x"], [1L, null], [null, "x"]], o => o.WithStrictOrdering());
    }

    [Fact]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
    public async Task Drops_a_row_with_null_in_a_not_null_column_Async()
    {
        var filename = $"{nameof(Can_handle_different_data_types)}_notnull.db";

        File.Delete(filename);

        var tree = new SqlTreeCreator()
            .AddDb("DataTypTestDB")
            .AddSchema("DataTypTestSchema")
            .AddTable("NotNull")
            .AddColumn("value", typeof(int), notNull: true)
            .Create();
        int? value = null;
        var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, typeof(int));
        FakeLogger<SqliteDataPortOutgoing> logger = new();
        using var outgoing = new SqliteDataPortOutgoing(new() { Filename = filename, Nodes = tree }, logger);

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        logger.LatestRecord.Message.Should().Match("The database rejected a row for *NotNull. The row is dropped.");
        logger.LatestRecord.Id.Should().Be(new EventId(4, "LogRowDropped"));
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public async Task Keeps_writing_after_a_rejected_value_Async()
    {
        var filename = $"{nameof(Keeps_writing_after_a_rejected_value_Async)}.db";
        File.Delete(filename);
        var tree = new SqlTreeCreator()
            .AddDb("RejectDB")
            .AddSchema("RejectSchema")
            .AddTable("doubles")
            .AddColumn("value", typeof(double))
            .Create();
        using var outgoing = new SqliteDataPortOutgoing(new() { Filename = filename, Nodes = tree }, Substitute.For<ILogger<SqliteDataPortOutgoing>>());
        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);

        await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, double.NaN, typeof(double)), TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, 1.5, typeof(double)), TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        var rows = await SqliteTestQueries.SelectAllAsync(filename, "doubles", TestContext.Current.CancellationToken);
        rows.Select(r => r[0]).Should().Equal(1.5);
    }

    public static TheoryData<Type, object, object> GetData()
        => new()
        {
            { typeof(string), "test", "test"},
            { typeof(int), 1 , 1},
            { typeof(long), 1000000L, 1000000L },
            { typeof(short), (short)2 , (short)2},
            { typeof(Guid), Guid.Empty, Guid.Empty.ToString() },
            { typeof(bool), true, 1 },
            { typeof(float), 0.5f, 0.5f },
            { typeof(double), 1.5d, 1.5d },
            { typeof(decimal), 1.55M, "1.55" },
            { typeof(DateTime), new DateTime(2024, 10, 23) , "2024-10-23 00:00:00"},
            { typeof(TimeSpan), TimeSpan.FromMinutes(5.5) , TimeSpan.FromMinutes(5.5).ToString()},
            { typeof(byte), (byte)2 , 2},
            { typeof(sbyte), (sbyte)-3, -3 },
            { typeof(char), 'A' , "A"},
            { typeof(byte[]), new byte[] { 1,1,1,1 }, new byte[] { 1,1,1,1 } },
        };
}
