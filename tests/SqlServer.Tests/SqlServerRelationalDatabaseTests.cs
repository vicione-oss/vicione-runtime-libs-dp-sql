using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqlServerRelationalDatabase_CreateDataRowsFromTemplate
{
    [Fact]
    public void Acceptance()
    {
        using DataTable dataTable = new();
        dataTable.Columns.Add("id");
        dataTable.Columns.Add("name");
        TableInfo table = new("MyDB", "dbo", "users");
        ColumnInfo idColumn = new(table, "id", false, false, typeof(Guid));
        ColumnInfo nameColumn = new(table, "name", false, false, typeof(string));
        List<RowData> rowData =
        [
            new(table, new() { { idColumn, 23 }, { nameColumn, "Test1" }, }),
            new(table, new() { { idColumn, 24 }, { nameColumn, "Test2" }, }),
        ];

        var dataRows = SqlServerRelationalDatabase.CreateDataRowsFromTemplate(dataTable, rowData);

        dataTable.Rows.Count.Should().Be(0);
        dataRows.Should().HaveCount(2).And.SatisfyRespectively(
            r =>
            {
                r["id"].Should().Be("23");
                r["name"].Should().Be("Test1");
            },
            r =>
            {
                r["id"].Should().Be("24");
                r["name"].Should().Be("Test2");
            });
    }
}

public class SqlServerRelationalDatabase_OpenConnectionAsync
{
    [Fact]
    public async Task Throws_when_multiple_databases_detected_in_single_database_mode()
    {
        var tree = new SqlTreeCreator()
            .AddDb("DB1").AddSchema("dbo").AddTable("t1").AddColumn("value", typeof(string))
            .AddDb("DB2").AddSchema("dbo").AddTable("t2").AddColumn("value", typeof(string))
            .Create();
        var communication = new SqlServerDataPortCommunication { Nodes = tree, DesignId = SqlServerRelationalDatabase.SingleDatabaseInstanceNodeId };
        using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

        await outgoing.Awaiting(o => o.ConnectAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SingleDatabaseInstance*");
    }
}

[Collection(SqlServerTestEnvironment.Name)]
public class SqlServerRelationalDatabase_SingleDatabaseInstance(SqlServerTestSystem sqlServer)
{
    [Fact]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    public async Task Can_create_structure()
    {
        var dbName = sqlServer.Communication.Database;
        var tree = new SqlTreeCreator()
            .AddDb(dbName)
            .AddSchema("SingleDbStructureSchema")
            .AddTable("values")
            .AddColumn("value", typeof(string))
            .Create();
        var communication = sqlServer.Communication with { Nodes = tree, DesignId = SqlServerRelationalDatabase.SingleDatabaseInstanceNodeId };
        using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new SqlConnection(sqlServer.Communication.ToConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var command = new SqlCommand("SELECT COUNT(*) FROM sys.schemas WHERE name = 'SingleDbStructureSchema'", connection);
        var count = (int)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Interoperability")]
    [SuppressMessage("Security", "CA2100:SQL-Abfragen auf Sicherheitsrisiken überprüfen")]
    public async Task Can_write_data()
    {
        var dbName = sqlServer.Communication.Database;
        var tree = new SqlTreeCreator()
            .AddDb(dbName)
            .AddSchema("SingleDbWriteSchema")
            .AddTable("values")
            .AddColumn("value", typeof(string))
            .Create();
        var data = RelationalDatabaseTestHelper.CreateTestData(tree, "test_value", typeof(string));
        var communication = sqlServer.Communication with { Nodes = tree, DesignId = SqlServerRelationalDatabase.SingleDatabaseInstanceNodeId };
        using var outgoing = new SqlServerDataPortOutgoing(communication, Substitute.For<ILogger<SqlServerDataPortOutgoing>>());

        await outgoing.ConnectAsync(TestContext.Current.CancellationToken);
        await outgoing.SendAsync(0, data, TestContext.Current.CancellationToken);
        await outgoing.DisconnectAsync(TestContext.Current.CancellationToken);

        using var connection = new SqlConnection(sqlServer.Communication.ToConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var command = new SqlCommand("SELECT value FROM [SingleDbWriteSchema].[values]", connection);
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        result.Should().Be("test_value");
    }
}
