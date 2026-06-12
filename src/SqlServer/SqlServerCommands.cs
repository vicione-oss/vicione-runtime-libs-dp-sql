using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Text;

namespace ViciOne.Suite.DataPort;

internal static class SqlServerCommands
{
    internal const string SelectAllDatabases = "SELECT name FROM sys.sysdatabases;";
    internal const string SelectAllSchemasWithInformation = """
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
    """;

    internal static string ReadDatabase(DbDataReader reader)
        => reader.GetFieldValue<string>(0);

    internal static SchemaInfo ReadSchemaInfo(DbDataReader reader)
    {
        var datatype = reader.IsDBNull(3) ? null : reader.GetFieldValue<string>(3);

        return new(
            reader.GetFieldValue<string>(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<string>(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<string>(2),
            reader.IsDBNull(3) ? null : GetDataTypePrecision(reader, datatype),
            reader.GetFieldValue<bool>(4),
            reader.GetFieldValue<bool>(5),
            reader.GetFieldValue<bool>(6));
    }

    private static string? GetDataTypePrecision(DbDataReader reader, string? dataType)
        => dataType switch
        {
            "decimal" => $"{dataType}({reader.GetFieldValue<byte>(7)},{reader.GetFieldValue<byte>(8)})",
            "varbinary" => $"{dataType}(MAX)",
            _ => dataType,
        };

    internal static string ComposeTableNameTwoPart(TableInfo tableInfo)
        => ComposeTableNameTwoPart(tableInfo.Schema, tableInfo.Table);

    internal static string ComposeTableNameTwoPart(string schema, string table)
        => $"{IdentifierQuoter.QuoteForSqlServer(schema)}.{IdentifierQuoter.QuoteForSqlServer(table)}";

    internal static string ComposeSelectStructureCommandText(string database)
    {
        StringBuilder commandText = new();
        commandText.AppendLine(ComposeUseDatabaseCommandText(database));
        commandText.Append(SelectAllSchemasWithInformation);
        return commandText.ToString();
    }

    internal static string ComposeCreateDatabasesCommandText(IReadOnlyCollection<Database> databases)
    {
        StringBuilder commandText = new();

        foreach (var database in databases)
            commandText.AppendLine(ComposeCreateDatabaseCommandText(database));

        return commandText.ToString();
    }

    internal static string ComposeCreateTablesCommandText(IReadOnlyCollection<Database> databases, bool singleDatabase = false)
    {
        StringBuilder commandText = new();

        foreach (var database in databases)
        {
            if (!singleDatabase)
                commandText.AppendLine(ComposeUseDatabaseCommandText(database));

            foreach (var schema in database.Schemas)
            {
                foreach (var table in schema.Tables)
                    commandText.AppendLine(ComposeCreateTableCommandText(schema, table));
            }
        }

        return commandText.ToString();
    }

    internal static string ComposeCreateDatabaseCommandText(Database database)
        => $"CREATE DATABASE {IdentifierQuoter.QuoteForSqlServer(database.Name)};";

    internal static string ComposeCreateSchemaCommandText(Schema schema)
        => $"CREATE SCHEMA {IdentifierQuoter.QuoteForSqlServer(schema.Name)};";

    internal static string ComposeUseDatabaseCommandText(Database database)
        => ComposeUseDatabaseCommandText(database.Name);

    internal static string ComposeUseDatabaseCommandText(string database)
        => $"USE {IdentifierQuoter.QuoteForSqlServer(database)};";

    internal static string ComposeCreateTableCommandText(Schema schema, Table table)
    {
        StringBuilder command = new();

        var tableName = ComposeTableNameTwoPart(schema.Name, table.Name);

        command.AppendFormat(CultureInfo.InvariantCulture, "CREATE TABLE {0} (", tableName);
        command.AppendLine();

        var last = table.Columns.Count - 1;
        var i = 0;

        foreach (var column in table.Columns)
        {
            command.Append(CultureInfo.InvariantCulture, $"{IdentifierQuoter.QuoteForSqlServer(column.Name)} {column.Type}");

            if (column.AutoIncrement)
                command.Append(" IDENTITY(1,1)");
            if (column.PrimaryKey)
                command.Append(" PRIMARY KEY");
            if (column.NotNull)
                command.Append(" NOT NULL");

            if (i != last)
                command.AppendLine(",");

            i++;
        }

        command.Append(");");

        return command.ToString();
    }
}
