using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Npgsql;
using NSubstitute;
using ViciOne.ManagedEngine.Runtime;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class PostgreSqlDataPortOutgoing_
{
    [Fact]
    public async Task DependencyInjectionProviderFactory_can_create_instance_Async()
    {
        await using var providerFactory = new ConstructorProviderFactory();

        var instance = providerFactory.CreateExternalOutgoing(
            new PostgreSqlDataPortCommunication(),
            new(),
            Substitute.For<ILoggerFactory>(),
            Substitute.For<INameResolver>(),
            AssemblyLoadContext.Default);

        instance.Should().NotBeNull().And.BeOfType<PostgreSqlDataPortOutgoing>();
    }

    [Collection(PostgreSqlTestEnvironment.Name)]
    public class Interoperability(PostgreSqlTestSystem sqlServer)
    {
        [Fact]
        [Trait("Category", "Interoperability")]
        [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen", Justification = "<Ausstehend>")]
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
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, Substitute.For<ILogger<PostgreSqlDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new NpgsqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = new NpgsqlCommand(ComposeSelectSchemaCommand("StructureSchema"), connection);
            var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            var result = new List<SchemaInfo>();

            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                result.Add(new(
                    await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken),
                    await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken),
                    await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(2, TestContext.Current.CancellationToken),
                    await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(3, TestContext.Current.CancellationToken),
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
                .AddColumn("value", typeof(int), primaryKey: true)
                .AddTable("NotNull")
                .AddColumn("value", typeof(int), notNull: true)
                .AddTable("AutoIcrement")
                .AddColumn("value", typeof(int), autoIncrement: true)
                .Create();
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, Substitute.For<ILogger<PostgreSqlDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new NpgsqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = new NpgsqlCommand(ComposeSelectSchemaCommand("ConstrainsSchema"), connection);
            var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            var result = new List<SchemaInfo>();

            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                result.Add(new(
                    await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken),
                    await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(1, TestContext.Current.CancellationToken),
                    await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(2, TestContext.Current.CancellationToken),
                    await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<string>(3, TestContext.Current.CancellationToken),
                    await reader.GetFieldValueAsync<bool>(4, TestContext.Current.CancellationToken),
                    await reader.GetFieldValueAsync<bool>(5, TestContext.Current.CancellationToken),
                    await reader.GetFieldValueAsync<bool>(6, TestContext.Current.CancellationToken)));
            }

            result.Should()
                .AllSatisfy(e => e.Schema.Should().Be("ConstrainsSchema"))
                .And.HaveCount(3)
                .And.Contain(e => e.ColumnIsPrimaryKey)
                .And.Contain(e => e.ColumnIsNotNull && !e.ColumnIsPrimaryKey && !e.ColumnIsAutoIncrement)
                .And.Contain(e => e.ColumnIsAutoIncrement);
        }

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
                .AddColumn("value", type, notNull: true)
                .Create();
            var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, type);
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, Substitute.For<ILogger<PostgreSqlDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new NpgsqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('"',
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name,
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId).Name);
            using var command = new NpgsqlCommand(query, connection);

            (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().BeEquivalentTo(value);
            await connection.CloseAsync();
        }

        [Fact]
        [Trait("Category", "Interoperability")]
        [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
        [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
        public async Task Drops_a_row_with_null_in_a_not_null_column_Async()
        {
            var tree = new SqlTreeCreator()
                .AddDb("DataTypTestDB")
                .AddSchema("DataTypTestSchema")
                .AddTable("NotNull")
                .AddColumn("value", typeof(int), notNull: true)
                .Create();
            int? value = null;
            var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, typeof(int));
            var communication = sqlServer.Communication with { Nodes = tree, };
            FakeLogger<PostgreSqlDataPortOutgoing> logger = new();
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, logger);

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            logger.LatestRecord.Message.Should().Match("The database rejected a row for *NotNull. The row is dropped.");
            logger.LatestRecord.Id.Should().Be(new EventId(4, "LogRowDropped"));
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
                .AddColumn("id", typeof(int), autoIncrement: true)
                .AddColumn("value", typeof(string))
                .Create();
            var data = RelationalDatabaseTestHelper.CreateTestData(tree, "test", typeof(string));
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, Substitute.For<ILogger<PostgreSqlDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new NpgsqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('"',
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name,
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId).Name);
            using var command = new NpgsqlCommand(query, connection);

            (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().BeEquivalentTo(1);
            await connection.CloseAsync();
        }

        [Theory]
        [MemberData(nameof(GetUnsignedData))]
        [Trait("Category", "Interoperability")]
        [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
        [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable", Justification = "Nicht möglich")]
        public async Task Writes_unsigned_integers_Async(Type type, object value, object expected)
        {
            var tree = new SqlTreeCreator()
                .AddDb("UnsignedDB")
                .AddSchema("UnsignedSchema")
                .AddTable(type.Name)
                .AddColumn("value", type, notNull: true)
                .Create();
            var data = RelationalDatabaseTestHelper.CreateTestData(tree, value, type);
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, Substitute.For<ILogger<PostgreSqlDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new NpgsqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = new NpgsqlCommand(RelationalDatabaseTestHelper.ComposeSelectTableQuery('"', type.Name, "UnsignedSchema"), connection);
            (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be(expected);
        }

        public static TheoryData<Type, object, object> GetUnsignedData()
            => new()
            {
                { typeof(ushort), ushort.MaxValue, (int)ushort.MaxValue },
                { typeof(uint), uint.MaxValue, (long)uint.MaxValue },
                { typeof(ulong), (ulong)long.MaxValue, long.MaxValue },
            };

        [Fact]
        [Trait("Category", "Interoperability")]
        [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
        public async Task Keeps_writing_after_a_duplicate_primary_key_Async()
        {
            var tree = new SqlTreeCreator()
                .AddDb("RejectDB")
                .AddSchema("RejectSchema")
                .AddTable("keyed")
                .AddColumn("id", typeof(int), primaryKey: true)
                .Create();
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new PostgreSqlDataPortOutgoing(communication, Substitute.For<ILogger<PostgreSqlDataPortOutgoing>>());
            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);

            await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, 1, typeof(int)), TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, 1, typeof(int)), TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, RelationalDatabaseTestHelper.CreateTestData(tree, 2, typeof(int)), TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new NpgsqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = new NpgsqlCommand("SELECT count(*) FROM \"RejectSchema\".\"keyed\";", connection);
            (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be(2L);
        }

        public static TheoryData<Type, object> GetData()
            => new()
            {
                { typeof(string), "test" },
                { typeof(int), 1 },
                { typeof(long), 1000000L },
                { typeof(short), (short)2 },
                { typeof(Guid), Guid.Empty },
                { typeof(bool), true },
                { typeof(float), 0.5f },
                { typeof(double), 1.5d },
                { typeof(decimal), 1.55M },
                { typeof(DateTime), new DateTime(2024, 10, 23) },
                { typeof(TimeSpan), TimeSpan.FromMinutes(5.5) },
                { typeof(byte), (byte)2 },
                { typeof(sbyte), (sbyte)-3 },
                { typeof(char), "A" },
                { typeof(byte[]), new byte[] { 1,1,1,1 } },
            };

        private static string ComposeSelectSchemaCommand(string schema)
            => $"""
                SELECT
                    s.schema_name AS schema,
                    t.table_name AS table,
                    c.column_name AS column,
                    c.data_type AS type,
                    CAST (CASE WHEN ccu.column_name = c.column_name THEN true ELSE false END AS boolean) AS primary_key,
                    CAST (CASE WHEN c.is_nullable = 'YES' THEN false ELSE true END AS boolean) AS not_null,
                    CAST (CASE WHEN c.is_identity = 'YES' THEN true ELSE false END AS boolean) AS auto_increment
                FROM information_schema.schemata AS s
                    LEFT JOIN information_schema.tables AS t ON s.schema_name = t.table_schema
                    LEFT JOIN information_schema.columns AS c  ON c.table_name = t.table_name AND c.table_schema = t.table_schema
                    LEFT JOIN information_schema.table_constraints AS tc  ON tc.table_name = t.table_name AND tc.table_schema = t.table_schema AND tc.constraint_type = 'PRIMARY KEY'
                    LEFT JOIN information_schema.constraint_column_usage AS ccu  ON ccu.table_name = t.table_name AND ccu.table_schema = t.table_schema AND tc.constraint_name = ccu.constraint_name
                WHERE
                    s.schema_name NOT IN ('pg_catalog', 'pg_toast', 'information_schema', 'public') AND
                    s.schema_name = '{schema}'
                """;
    }
}
