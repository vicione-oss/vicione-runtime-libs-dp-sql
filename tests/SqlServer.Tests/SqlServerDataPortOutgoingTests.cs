using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using ViciOne.ManagedEngine.Runtime;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqlServerDataPortOutgoing_
{
    [Fact]
    public async Task DependencyInjectionProviderFactory_can_create_instance_Async()
    {
        await using var providerFactory = new ConstructorProviderFactory();

        var instance = providerFactory.CreateExternalOutgoing(
            new SqlServerDataPortCommunication(),
            new(),
            Substitute.For<ILoggerFactory>(),
            Substitute.For<INameResolver>(),
            AssemblyLoadContext.Default);

        instance.Should().NotBeNull().And.BeOfType<SqlServerDataPortOutgoing>();
    }

    [Collection(SqlServerTestEnvironment.Name)]
    public class Interoperability(SqlServerTestSystem sqlServer)
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
                .AddTable("decimal")
                .AddColumn("value", typeof(decimal))
                .Create();
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new SqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = new SqlCommand(ComposeSelectSchemaCommand("StructureSchema"), connection);
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
                .And.Contain(e => e.Table == "decimal")
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
            using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            using var connection = new SqlConnection(sqlServer.Communication.ToConnectionString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = new SqlCommand(ComposeSelectSchemaCommand("ConstrainsSchema"), connection);
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
                .And.HaveCount(3)
                .And.Contain(e => e.ColumnIsPrimaryKey)
                .And.Contain(e => e.ColumnIsNotNull && !e.ColumnIsPrimaryKey && !e.ColumnIsAutoIncrement)
                .And.Contain(e => e.ColumnIsAutoIncrement);
        }

        private static string? GetDataTypePrecision(DbDataReader reader, string? dataType)
            => dataType switch
            {
                "decimal" => $"{dataType}({reader.GetFieldValue<byte>(7)},{reader.GetFieldValue<byte>(8)})",
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
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"{communication.Host},{communication.Port}",
                Password = communication.Password,
                UserID = communication.UserId,
                InitialCatalog = tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.DatabaseNodeId).Name,
                TrustServerCertificate = true
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('"',
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name,
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId).Name);

            using var command = new SqlCommand(query, connection);

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
            var communication = sqlServer.Communication with { Nodes = tree, };
            FakeLogger<SqlServerDataPortOutgoing> logger = new();
            using var outgoing = new SqlServerDataPortOutgoing(communication, logger);

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
                .AddColumn("id", typeof(int), autoIncrement: true)
                .AddColumn("value", typeof(string))
                .Create();
            var data = RelationalDatabaseTestHelper.CreateTestData(tree, "test", typeof(string));
            var communication = sqlServer.Communication with { Nodes = tree, };
            using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

            await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
            await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
            await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"{communication.Host},{communication.Port}",
                Password = communication.Password,
                UserID = communication.UserId,
                InitialCatalog = tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.DatabaseNodeId).Name,
                TrustServerCertificate = true
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            var query = RelationalDatabaseTestHelper.ComposeSelectTableQuery('"',
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.TableNodeId).Name,
                tree.First(t => t.DesignId == RelationalDatabaseNodeDesignId.SchemaNodeId).Name);

            using var command = new SqlCommand(query, connection);

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
            var communication = sqlServer.Communication with { Nodes = tree, };
            using (var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>()))
            {
                await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
                await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
                await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);
            }
            using var reconnected = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

            var act = () => reconnected.ConnectAsync(TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
        }

        public static TheoryData<Type, object> GetData()
            => new()
            {
                { typeof(string), "test" },
                { typeof(int), 1 },
                { typeof(long), 1000000L },
                { typeof(short), (short)2 },
                { typeof(bool), true },
                { typeof(float), 0.5f },
                { typeof(double), 1.5d },
                { typeof(decimal), 1.55 },
                { typeof(DateTime), new DateTime(2024, 10, 23) },
                { typeof(TimeSpan), TimeSpan.FromMinutes(5.5) },
                { typeof(byte), (byte)2 },
                { typeof(sbyte), (sbyte)-3 },
                { typeof(char), "A" },
                { typeof(ushort), (ushort)7 },
                { typeof(uint), (uint)7 },
                { typeof(Guid), Guid.Empty },
                { typeof(byte[]), new byte[] { 1, 1, 1, 1 } },
            };

        private static string ComposeSelectSchemaCommand(string schema)
            => $"""
                Use "StructureDB";
                SELECT
                    s.name AS 'schema',
                    t.name AS 'table',
                    c.name AS 'column',
                    ty.name AS 'type',
                    ISNULL(i.is_primary_key, 'FALSE') AS 'primary key',
                    CASE c.is_nullable WHEN 'TRUE' THEN CAST(0 as bit) ELSE CAST(1 as bit) END AS 'not null',
                    ISNULL(c.is_identity, 'FALSE')  AS 'auto increment',
                    c.precision as 'precision',
                    c.scale as 'scale'
                FROM sys.schemas AS s
                    LEFT JOIN sys.tables t ON s.schema_id = t.schema_id
                    LEFT JOIN sys.columns c ON c.object_id = t.object_id
                    LEFT JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                    LEFT JOIN sys.index_columns ic  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    LEFT JOIN sys.indexes i ON c.object_id = i.object_id AND c.column_id = ic.column_id
                WHERE
                    s.name = '{schema}'
                """;
    }
}
