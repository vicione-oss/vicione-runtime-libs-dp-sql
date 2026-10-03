using System;
using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class MySqlCommands_ReadSchemaInfo
{
    [Fact]
    public void Reads_the_normalized_column_type()
    {
        using var table = CreateSchemaTable("int(10) unsigned");
        using var reader = table.CreateDataReader();
        reader.Read();

        var schemaInfo = MySqlCommands.ReadSchemaInfo(reader);

        schemaInfo.ColumnType.Should().Be("int unsigned");
    }

    [Fact]
    public void Reads_a_database_without_tables()
    {
        using var table = CreateSchemaTable(null);
        using var reader = table.CreateDataReader();
        reader.Read();

        var schemaInfo = MySqlCommands.ReadSchemaInfo(reader);

        schemaInfo.Should().Be(new SchemaInfo("schema", null, null, null, false, false, false));
    }

    private static DataTable CreateSchemaTable(string? columnType)
    {
        DataTable table = new();
        table.Columns.Add("schema", typeof(string));
        table.Columns.Add("table", typeof(string));
        table.Columns.Add("column", typeof(string));
        table.Columns.Add("type", typeof(string));
        table.Columns.Add("primary_key", typeof(bool));
        table.Columns.Add("not_null", typeof(bool));
        table.Columns.Add("auto_increment", typeof(bool));

        if (columnType is null)
            table.Rows.Add("schema", null, null, null, false, false, false);
        else
            table.Rows.Add("schema", "table", "id", columnType, true, true, false);

        return table;
    }
}

public class MySqlCommands_NormalizeColumnType
{
    [Theory]
    [InlineData("tinyint(1)", "bool")]
    [InlineData("tinyint(4)", "tinyint")]
    [InlineData("tinyint(3) unsigned", "tinyint unsigned")]
    [InlineData("smallint(5) unsigned", "smallint unsigned")]
    [InlineData("int(11)", "int")]
    [InlineData("int unsigned", "int unsigned")]
    [InlineData("bigint(20) unsigned", "bigint unsigned")]
    [InlineData("longtext", "json")]
    [InlineData("json", "json")]
    [InlineData("varchar(255)", "varchar(255)")]
    [InlineData("varbinary(8000)", "varbinary(8000)")]
    [InlineData("decimal(65,30)", "decimal(65,30)")]
    [InlineData("TEXT", "text")]
    [InlineData("datetime", "datetime(6)")]
    [InlineData("datetime(6)", "datetime(6)")]
    public void Matches_the_type_names_columns_are_created_with(string columnType, string expected)
        => MySqlCommands.NormalizeColumnType(columnType).Should().Be(expected);

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(int))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(string))]
    [InlineData(typeof(byte[]))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(DateTime))]
    public void Keeps_the_created_type_name(Type type)
    {
        var created = MySqlExtensions.ConvertType(type);

        MySqlCommands.NormalizeColumnType(created).Should().Be(created);
    }
}

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
        CREATE SCHEMA IF NOT EXISTS `test1`;
        CREATE SCHEMA IF NOT EXISTS `test2`;
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
