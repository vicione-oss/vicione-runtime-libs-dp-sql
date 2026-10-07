using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using ViciOne.ManagedEngine.ExternalCommunication;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class RelationalDatabase_
{
    [Fact]
    public async Task Publishes_values_Async()
    {
        IEnumerable<RowData> data = [];
        using TestDatabase database = new();
        database.ValuesPublished += d => data = d;

        await database.ConnectAsync(TestContext.Current.CancellationToken);
        database.Send(
        [
            new()
            {
                Channel = "name",
                Value = "Test",
            },
            new()
            {
                Channel = "value",
                Value = 23,
            },
        ]);
        await database.DisconnectAsync(TestContext.Current.CancellationToken);

        data.Should().ContainSingle().Which.Columns.Should().HaveCount(2).And.SatisfyRespectively(
            c =>
            {
                c.Key.Name.Should().Be("name");
                c.Value.Should().Be("Test");
            },
            c =>
            {
                c.Key.Name.Should().Be("props");
                JsonNode.Parse(c.Value.Should().BeOfType<string>().Which)!["value"]!.GetValue<int>().Should().Be(23);
            });
    }

    [Fact]
    public async Task Drops_a_row_the_database_rejects_and_writes_the_others_Async()
    {
        FakeLogger logger = new();
        using MetricCollector<long> droppedRows = new(null, RelationalDatabase.MeterName, RelationalDatabase.RowsDroppedInstrumentName);
        List<object?> written = [];
        using TestDatabase database = new(logger, rows => rows.Any(r => Equals(Name(r), "rejected"))
            ? throw new RejectedRowException()
            : Task.CompletedTask);
        database.ValuesPublished += rows => written.AddRange(rows.Select(Name));

        await database.ConnectAsync(TestContext.Current.CancellationToken);
        database.Send([new() { Channel = "name", Value = "first" }]);
        database.Send([new() { Channel = "name", Value = "rejected" }]);
        database.Send([new() { Channel = "name", Value = "last" }]);
        await database.DisconnectAsync(TestContext.Current.CancellationToken);

        written.Should().Equal("first", "last");
        logger.Collector.GetSnapshot().Should().ContainSingle(e => e.Level == LogLevel.Error).Which.Message
            .Should().Be("The database rejected a row for default.default. The row is dropped.");
        droppedRows.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public async Task Publishes_a_batch_again_after_a_transient_failure_Async()
    {
        FakeLogger logger = new();
        var failures = 1;
        TaskCompletionSource<IEnumerable<RowData>> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var transient = Substitute.For<DbException>();
        transient.IsTransient.Returns(true);
        using TestDatabase database = new(logger, rows => failures-- > 0 ? throw transient : Task.CompletedTask);
        database.ValuesPublished += rows => published.TrySetResult(rows);

        await database.ConnectAsync(TestContext.Current.CancellationToken);
        database.Send([new() { Channel = "name", Value = "kept" }]);
        var rows = await published.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await database.DisconnectAsync(TestContext.Current.CancellationToken);

        rows.Select(Name).Should().Equal("kept");
        logger.Collector.GetSnapshot().Should().Contain(e => e.Message == "The database did not accept the values. They stay queued and are published again.")
            .And.NotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Closes_and_retries_when_the_connection_broke_during_the_publish_Async()
    {
        var failures = 1;
        TaskCompletionSource published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = Substitute.For<DbConnection>();
        connection.State.Returns(ConnectionState.Broken);
        using TestDatabase database = new(publish: _ => failures-- > 0 ? throw new InvalidOperationException("Connection is not open.") : Task.CompletedTask)
        {
            TestConnection = connection,
        };
        database.ValuesPublished += _ => published.TrySetResult();

        await database.ConnectAsync(TestContext.Current.CancellationToken);
        database.Send([new() { Channel = "name", Value = "kept" }]);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await database.DisconnectAsync(TestContext.Current.CancellationToken);

        await connection.Received().CloseAsync();
    }

    [Fact]
    public async Task Keeps_the_rows_when_the_database_fails_for_another_reason_Async()
    {
        FakeLogger logger = new();
        var failures = 1;
        TaskCompletionSource<IEnumerable<RowData>> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using TestDatabase database = new(logger, rows => failures-- > 0 ? throw new InvalidOperationException("Table 'default' doesn't exist") : Task.CompletedTask);
        database.ValuesPublished += rows => published.TrySetResult(rows);

        await database.ConnectAsync(TestContext.Current.CancellationToken);
        database.Send([new() { Channel = "name", Value = "kept" }]);
        var rows = await published.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await database.DisconnectAsync(TestContext.Current.CancellationToken);

        rows.Select(Name).Should().Equal("kept");
        logger.Collector.GetSnapshot().Should().NotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Finds_rejected_rows_without_publishing_each_row_on_its_own_Async()
    {
        var rows = CreateRows(64);
        List<object?> written = [];
        var calls = 0;
        using TestDatabase database = new(publish: batch =>
        {
            calls++;
            return batch.Any(r => Equals(Name(r), "row 17")) ? throw new RejectedRowException() : Task.CompletedTask;
        });
        database.ValuesPublished += batch => written.AddRange(batch.Select(Name));

        var published = await database.PublishAsync(rows, TestContext.Current.CancellationToken);

        published.Should().BeTrue();
        written.Should().Equal(rows.Select(Name).Where(n => !Equals(n, "row 17")));
        calls.Should().Be(13);
    }

    [Fact]
    public async Task Does_not_write_a_row_twice_when_the_database_fails_while_rejected_rows_are_isolated_Async()
    {
        FakeLogger logger = new();
        using MetricCollector<long> droppedRows = new(null, RelationalDatabase.MeterName, RelationalDatabase.RowsDroppedInstrumentName);
        var rows = CreateRows(8);
        List<object?> written = [];
        var unreachable = true;
        using TestDatabase database = new(logger, batch =>
        {
            if (batch.Any(r => Equals(Name(r), "row 5")))
                throw new RejectedRowException();
            if (unreachable && batch.Any(r => Equals(Name(r), "row 6")))
            {
                unreachable = false;
                throw new InvalidOperationException("Connection is not open.");
            }

            return Task.CompletedTask;
        });
        database.ValuesPublished += batch => written.AddRange(batch.Select(Name));

        var firstAttempt = await database.PublishAsync(rows, TestContext.Current.CancellationToken);
        var retry = await database.PublishAsync(rows, TestContext.Current.CancellationToken);

        firstAttempt.Should().BeFalse();
        retry.Should().BeTrue();
        written.Should().BeEquivalentTo(rows.Select(Name).Where(n => !Equals(n, "row 5")));
        logger.Collector.GetSnapshot().Where(e => e.Level == LogLevel.Error).Should().ContainSingle();
        droppedRows.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    private static List<RowData> CreateRows(int count)
    {
        TableInfo table = new("default", "default", "default");
        ColumnInfo name = new(table, "name", false, false, typeof(string));
        return [.. Enumerable.Range(0, count).Select(i => new RowData(table, new() { [name] = $"row {i}" }))];
    }

    private static object? Name(RowData row)
        => row.Columns.Single(c => c.Key.Name == "name").Value;

    internal sealed record TestCommunication : RelationalDatabaseCommunication
    { }

    internal sealed class TestDatabase(ILogger? logger = null, Func<IReadOnlyCollection<RowData>, Task>? publish = null)
        : RelationalDatabase(CreateCommunication(), logger ?? Substitute.For<ILogger>())
    {
        private static readonly Node s_idNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "ID",
            ValueType = typeof(int),
        };
        private static readonly Node s_nameNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "name",
            ValueType = typeof(string),
            AffectedChannels = ["name"],
            TransferredChannels = ["name"],
        };
        private static readonly Node s_propsNode = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "props",
            TransferredChannels = ["value"],
        };
        private static readonly Node s_valueNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = s_propsNode.Id,
            DesignId = RelationalDatabaseNodeDesignId.JsonStructureNodeId,
            Name = "value",
            ValueType = typeof(int),
            AffectedChannels = ["value"],
        };

        public event Action<IEnumerable<RowData>>? ValuesPublished;

        public DbConnection? TestConnection { get; init; }

        protected override DbConnection? Connection => TestConnection;

        private static TestCommunication CreateCommunication()
            => new()
            {
                Nodes = [s_idNode, s_nameNode, s_propsNode, s_valueNode,],
            };

        protected override Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Database>>(
            [
                new("default",
                [
                    new("default",
                    [
                        new("default",
                        [
                            new("ID", "int", true, false, true),
                            new("name", "string", false, false, false),
                            new("props", "json", false, false, false),
                        ]),
                    ]),
                ]),
            ]);

        protected override IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes)
            => [
                new("default",
                [
                    new("default",
                    [
                        new("default",
                        [
                            new("ID", "int", true, false, true),
                            new("name", "string", false, false, false),
                            new("props", "json", false, false, false),
                        ]),
                    ]),
                ]),
            ];

        protected override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
            => new()
            {
                { s_idNode.Id, new ColumnInfo(new ("default", "default", "default"), "ID", false, false, typeof(Guid)) },
                { s_nameNode.Id, new ColumnInfo(new ("default", "default", "default"), "name", false, false, typeof(string)) },
                { s_propsNode.Id, new ColumnInfo(new ("default", "default", "default"), "props", true, false, typeof(string)) },
            };

        protected override async Task PublishRowsAsync(IReadOnlyCollection<RowData> rows, CancellationToken cancellationToken)
        {
            if (publish is not null)
                await publish(rows);

            ValuesPublished?.Invoke(rows);
        }

        protected override bool IsRejectedByDatabase(Exception exception) => exception is RejectedRowException;

        protected override Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task CloseConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases, IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    internal sealed class RejectedRowException : Exception
    {
        public RejectedRowException()
        { }

        public RejectedRowException(string message) : base(message)
        { }

        public RejectedRowException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}

public class ContainsInappropriateSign
{
    [Theory]
    [InlineData("MyColumn")]
    [InlineData("Main.MyColumn")]
    [InlineData("Main.MyColumn-Sub")]
    [InlineData("Hello+World#2303")]
    [InlineData("Column 'Name'")]
    [InlineData("\"Quoted\"")]
    [InlineData("Hello World")]
    [InlineData("$100")]
    [InlineData("%discount")]
    [InlineData("a,b")]
    [InlineData("a;b")]
    [InlineData("a`b")]
    public void Returns_false_if_value_is_appropriate(string value)
        => RelationalDatabase.ContainsInappropriateSign(value).Should().BeFalse();

    [Theory]
    [InlineData("Hello\n")]
    [InlineData("Hello\r")]
    [InlineData("Hello\tWorld")]
    [InlineData("Hello\b")]
    [InlineData("Hello\0")]
    [InlineData("Hello\x01")]
    [InlineData("Hello\x1F")]
    [InlineData("Hello\x7F")]
    public void Returns_true_if_value_is_inappropriate(string value)
        => RelationalDatabase.ContainsInappropriateSign(value).Should().BeTrue();
}

public class ValidateNodeNames
{
    [Fact]
    public void Throws_with_names_and_ids()
    {
        var nodes = new Node[]
        {
            new()
            {
                Id = Guid.Parse("bc6ba8c1-dfe7-492c-aacd-9497d10f5a7a"),
                Name = "Test\x01",
            },
            new()
            {
                Id = Guid.Parse("5ac4ed19-8ba1-43f3-b042-79b6457b98d2"),
                Name = "Hello\n",
            },
        };
        var act = () => RelationalDatabase.ValidateNodeNames(nodes);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*names*inappropriate*Test\x01*bc6ba8c1-dfe7-492c-aacd-9497d10f5a7a*Hello\n*5ac4ed19-8ba1-43f3-b042-79b6457b98d2*");
    }

    [Fact]
    public void Throws_when_name_exceeds_max_length()
    {
        var longName = new string('a', 65);
        var nodes = new Node[]
        {
            new()
            {
                Id = Guid.Parse("bc6ba8c1-dfe7-492c-aacd-9497d10f5a7a"),
                Name = longName,
            },
        };
        var act = () => RelationalDatabase.ValidateNodeNames(nodes, maxIdentifierLength: 64);

        act.Should().Throw<InvalidOperationException>().WithMessage("*names*inappropriate*");
    }

    [Fact]
    public void Does_not_throw_when_name_equals_max_length()
    {
        var name = new string('a', 64);
        var nodes = new Node[]
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = name,
            },
        };
        var act = () => RelationalDatabase.ValidateNodeNames(nodes, maxIdentifierLength: 64);

        act.Should().NotThrow();
    }
}

public class ComposeMessage
{
    [Fact]
    public void Acceptance()
    {
        Table users = new("users", [new("ID", string.Empty, true, false, false), new("name", string.Empty, false, false, false), new("age", string.Empty, false, false, false),]);
        Schema schema = new("public", [users,]);
        var databases = new Database[]
        {
            new("mydb", [schema,]),
        };

        var message = RelationalDatabase.ComposeMessage(databases);

        message.Should().Match("*changes*expect*mydb.public.users.ID*mydb.public.users.name*mydb.public.users.age*");
    }
}

public class PrepareRowData
{
    [Fact]
    public void Can_handle_json_structure()
    {
        Node columnNode = new()
        {
            Id = Guid.NewGuid(),
            Name = "JSON column node"
        };
        Node apartmentNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = columnNode.Id,
            Name = "apartment node"
        };
        Node kitchenNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = apartmentNode.Id,
            Name = "kitchen node"
        };
        Node livingRoomNode = new()
        {
            Id = Guid.NewGuid(),
            ParentId = apartmentNode.Id,
            Name = "living room node"
        };
        TableInfo table = new("default", "default", "default");
        ColumnInfo column = new(table, "JSON column node", true, false, null);
        Dictionary<string, ColumnInfo> channelColumns = new()
        {
            { kitchenNode.Id.ToString(), column },
            { livingRoomNode.Id.ToString(), column },
        };
        Dictionary<string, INode[]> routes = new()
        {
            { kitchenNode.Id.ToString(), [apartmentNode, kitchenNode] },
            { livingRoomNode.Id.ToString(), [apartmentNode, livingRoomNode] },
        };
        List<ExternalValue> values =
        [
            new()
            {
                Channel = kitchenNode.Id.ToString(),
                Value = 1,
            },
            new()
            {
                Channel = livingRoomNode.Id.ToString(),
                Value = 2,
            },
        ];

        var data = RelationalDatabase.PrepareRowData(values, channelColumns, routes).ToArray();

        data.Should().ContainSingle().Which.Columns.Should().ContainSingle().Which.Value.Should().Be("""
        {
          "apartment node": {
            "kitchen node": 1,
            "living room node": 2
          },
          "$meta": {
            "apartment node": {
              "kitchen node": "System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e",
              "living room node": "System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e"
            }
          }
        }
        """.ReplaceLineEndings());
    }

    [Theory]
    [MemberData(nameof(GetSpecialSingleValues))]
    [SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable",
        Justification = "Die zu testenden Objekte sind zu unterschiedlich um sie auf einen serialisierbaren Typ zu reduzieren")]
    public void Can_handle_single_value(object? value, object? expected, bool isUnknown)
    {
        Node columnNode = new()
        {
            Id = Guid.NewGuid(),
            Name = "JSON column node"
        };
        TableInfo table = new("default", "default", "default");
        ColumnInfo column = new(table, "JSON column node", false, isUnknown, null);
        Dictionary<string, ColumnInfo> channelColumns = new()
        {
            { columnNode.Id.ToString(), column },
        };
        List<ExternalValue> values =
        [
            new()
            {
                Channel = columnNode.Id.ToString(),
                Value = value,
            },
        ];

        var data = RelationalDatabase.PrepareRowData(values, channelColumns, []).ToArray();

        data.Should().ContainSingle().Which.Columns.Should().ContainSingle().Which.Value.Should().Be(expected);
    }

    private sealed class Test
    {
        public int TestValue { get; set; }
    }

    public static TheoryData<object?, object?, bool> GetSpecialSingleValues()
        => new()
        {
            { null, DBNull.Value, false },
            { 23, 23, false },
            { new Test { TestValue = 2, }, "{\"TestValue\":2}", true },
        };
}

public class DisableStructureValidation_
{
    [Fact]
    public async Task ConnectAsync_does_not_call_CreateMissingStructure_when_DisableStructureValidation_is_true()
    {
        using var db = new DisabledValidationDatabase();
        // CreateMissingStructureAsync throws if called; ConnectAsync must not throw
        await db.ConnectAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConnectAsync_throws_on_column_mismatch_when_DisableStructureValidation_is_false()
    {
        using var db = new MismatchDatabase();

        var task = () => db.ConnectAsync(TestContext.Current.CancellationToken);

        await task.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    private static readonly Node s_columnNode = new()
    {
        Id = Guid.NewGuid(),
        DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
        Name = "ID",
        ValueType = typeof(int),
    };

    private sealed record DisabledValidationCommunication : RelationalDatabaseCommunication
    { }

    /// <summary>
    /// Database with DisableStructureValidation = true.
    /// GetExistingStructureAsync returns empty (simulates missing tables).
    /// CreateMissingStructureAsync throws — proving it is never invoked.
    /// GetExpectedStructure returns a structure that differs from existing — proving mismatch guard is also skipped.
    /// </summary>
    private sealed class DisabledValidationDatabase : RelationalDatabase
    {
        public DisabledValidationDatabase() : base(CreateCommunication(), Substitute.For<ILogger>()) { }

        private static DisabledValidationCommunication CreateCommunication()
            => new()
            {
                DisableStructureValidation = true,
                Nodes = [s_columnNode],
            };

        protected override Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Database>>([]);

        protected override IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes)
            => [
                new("default",
                [
                    new("default",
                    [
                        new("default",
                        [
                            new("ID", "int", true, false, true),
                            new("extra", "string", false, false, false),
                        ]),
                    ]),
                ]),
            ];

        protected override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
            => new()
            {
                { s_columnNode.Id, new ColumnInfo(new("default", "default", "default"), "ID", false, false, typeof(int)) },
            };

        protected override Task PublishRowsAsync(IReadOnlyCollection<RowData> rows, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task CloseConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases, IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken)
            => throw new InvalidOperationException("CreateMissingStructureAsync must not be called when DisableStructureValidation is true.");
    }

    /// <summary>
    /// Database with DisableStructureValidation = false (default) and mismatched columns.
    /// ConnectAsync must throw InvalidOperationException.
    /// </summary>
    private sealed class MismatchDatabase : RelationalDatabase
    {
        private static readonly Node s_node = new()
        {
            Id = Guid.NewGuid(),
            DesignId = RelationalDatabaseNodeDesignId.ColumnNodeId,
            Name = "ID",
            ValueType = typeof(int),
        };

        public MismatchDatabase() : base(CreateCommunication(), Substitute.For<ILogger>()) { }

        private static DisabledValidationCommunication CreateCommunication()
            => new()
            {
                DisableStructureValidation = false,
                Nodes = [s_node],
            };

        protected override Task<IReadOnlyCollection<Database>> GetExistingStructureAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Database>>(
            [
                new("default",
                [
                    new("default",
                    [
                        new("default",
                        [
                            new("ID", "int", true, false, true),
                            new("extra", "string", false, false, false),
                        ]),
                    ]),
                ]),
            ]);

        protected override IReadOnlyCollection<Database> GetExpectedStructure(IReadOnlyCollection<INode> nodes)
            => [
                new("default",
                [
                    new("default",
                    [
                        new("default",
                        [
                            new("ID", "int", true, false, true),
                            new("different_column", "string", false, false, false),
                        ]),
                    ]),
                ]),
            ];

        protected override Dictionary<Guid, ColumnInfo> CreateColumnInfos(IReadOnlyCollection<INode> nodes)
            => new()
            {
                { s_node.Id, new ColumnInfo(new("default", "default", "default"), "ID", false, false, typeof(int)) },
            };

        protected override Task PublishRowsAsync(IReadOnlyCollection<RowData> rows, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task OpenConnectionAsync(IReadOnlyCollection<ColumnInfo> columnInfos, CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task CloseConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task CreateMissingStructureAsync(IReadOnlyCollection<Database> missingTables, IReadOnlyCollection<Database> missingDatabases, IReadOnlyCollection<Database> missingSchemas, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
