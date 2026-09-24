using System.Data;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqlServerCommands_ReadSchemaInfo
{
    [Theory]
    [InlineData("nvarchar", (short)900, "nvarchar(450)")]
    [InlineData("nvarchar", (short)-1, "nvarchar(MAX)")]
    [InlineData("text", (short)16, "text")]
    public void Reads_nvarchar_with_its_length_in_characters(string dataType, short maxLengthInBytes, string expectedType)
    {
        using var table = CreateSchemaTable(dataType, maxLengthInBytes);
        using var reader = table.CreateDataReader();
        reader.Read();

        var schemaInfo = SqlServerCommands.ReadSchemaInfo(reader);

        schemaInfo.ColumnType.Should().Be(expectedType);
    }

    private static DataTable CreateSchemaTable(string dataType, short maxLengthInBytes)
    {
        DataTable table = new();
        table.Columns.Add("schema", typeof(string));
        table.Columns.Add("table", typeof(string));
        table.Columns.Add("column", typeof(string));
        table.Columns.Add("type", typeof(string));
        table.Columns.Add("primary key", typeof(bool));
        table.Columns.Add("not null", typeof(bool));
        table.Columns.Add("auto increment", typeof(bool));
        table.Columns.Add("precision", typeof(byte));
        table.Columns.Add("scale", typeof(byte));
        table.Columns.Add("length", typeof(short));
        table.Rows.Add("schema", "table", "id", dataType, true, true, false, null, null, maxLengthInBytes);
        return table;
    }
}

public class SqlServerCommands_ComposeCreateTableCommandText
{
    [Fact]
    public void Acceptance()
    {
        Table table = new("users",
        [
            new("id", "int", true, true, true),
            new("name", "text", false, false, false),
            new("age", "int", false, true, false),
        ]);
        Schema schema = new("dbo", [table,]);

        SqlServerCommands.ComposeCreateTableCommandText(schema, table).Should().Be("""
        CREATE TABLE [dbo].[users] (
        [id] int IDENTITY(1,1) PRIMARY KEY NOT NULL,
        [name] text,
        [age] int NOT NULL);
        """.ReplaceLineEndings());
    }
}

public class SqlServerCommands_ComposeTableNameTwoPart
{
    [Fact]
    public void Returns_two_part_name()
    {
        TableInfo tableInfo = new("MyDB", "dbo", "users");

        SqlServerCommands.ComposeTableNameTwoPart(tableInfo).Should().Be("[dbo].[users]");
    }
}
