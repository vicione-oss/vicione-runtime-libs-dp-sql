using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MySqlCommands_ComposeCreateTableCommandText
{
    [Fact]
    public void Acceptance()
    {
        Table table = new("users",
        [
            new("id", "int", true, true, true),
            new("name", "text", false, false, false),
        ]);

        Schema schema = new("dbo", [table,]);

        MySqlCommands.ComposeCreateTableCommandText(schema, table).Should().Be("""
        CREATE TABLE `dbo`.`users` (
        `id` int AUTO_INCREMENT PRIMARY KEY NOT NULL,
        `name` text);
        """.ReplaceLineEndings());
    }
}

public class MySqlCommands_ComposeInsertCommand
{
    [Fact]
    public void Acceptance()
    {
        var tableInfo = new TableInfo("def", "test", "test_table");
        var columnInfos = new List<ColumnInfo>() { new(tableInfo, "test_column", false, false, null) };

        MySqlCommands.ComposeInsertCommand(tableInfo, columnInfos).Should().Be("INSERT INTO `test`.`test_table` (`test_column`) VALUES (@test_column);");
    }
}

public class MySqlCommands_ComposeCreateStructureCommandText
{
    [Fact]
    public void Acceptance()
    {
        Table table = new("table_name1",
        [
            new("id", "int", false, false, false),
            new("name", "text", false, false, false),
        ]);

        Schema schema = new("test1", [table,]);
        Schema schema2 = new("test2", [table,]);
        Schema schema3 = new("test3", [table,]);

        var missingSchemasSchemas = new List<Schema>() { schema, schema2 };
        var missingTablesSchemas = new List<Schema>() { schema, schema2, schema3 };
        var missingSchemas = new List<Database>() { new(string.Empty, missingSchemasSchemas) };
        var missingTables = new List<Database>() { new(string.Empty, missingTablesSchemas), };

        MySqlCommands.ComposeCreateStructureCommandText(missingSchemas, missingTables).Should().Be("""
        CREATE SCHEMA `test1`;
        CREATE SCHEMA `test2`;
        CREATE TABLE `test1`.`table_name1` (
        `id` int,
        `name` text);
        CREATE TABLE `test2`.`table_name1` (
        `id` int,
        `name` text);
        CREATE TABLE `test3`.`table_name1` (
        `id` int,
        `name` text);
        """.ReplaceLineEndings());
    }
}
