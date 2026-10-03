using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ViciOne.Suite.DataPort;

internal static partial class MySqlCommands
{
    private const string SelectAllSchemasWithInformation = """
    SELECT
        s.SCHEMA_NAME AS "schema",
        c.TABLE_NAME AS "table",
        c.COLUMN_NAME AS "column",
        c.COLUMN_TYPE AS "type",
        CASE WHEN c.COLUMN_KEY = 'PRI' THEN true ELSE false END AS "primary_key",
        CASE WHEN c.IS_NULLABLE = 'NO' THEN true ELSE false END AS "not_null",
        CASE WHEN c.EXTRA LIKE '%auto_increment%' THEN true ELSE false END AS "auto_increment"
    FROM information_schema.schemata AS s
    LEFT JOIN information_schema.columns AS c ON c.TABLE_SCHEMA = s.SCHEMA_NAME
    WHERE s.SCHEMA_NAME NOT IN ('information_schema', 'mysql', 'sys', 'performance_schema')
    """;

    internal static SchemaInfo ReadSchemaInfo(DbDataReader reader)
        => new(
            reader.GetFieldValue<string>(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<string>(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<string>(2),
            reader.IsDBNull(3) ? null : NormalizeColumnType(reader.GetFieldValue<string>(3)),
            reader.GetFieldValue<bool>(4),
            reader.GetFieldValue<bool>(5),
            reader.GetFieldValue<bool>(6));

    // Brings COLUMN_TYPE to the names MySqlExtensions creates columns with: bool is stored as tinyint(1),
    // MariaDB reports integer display widths and stores json as longtext.
    [SuppressMessage("Globalization", "CA1308:Zeichenfolgen in Großbuchstaben normalisieren")]
    internal static string NormalizeColumnType(string columnType)
    {
        var type = columnType.ToLowerInvariant();

        return type switch
        {
            "tinyint(1)" => "bool",
            "longtext" => "json",
            _ => IntegerDisplayWidth().Replace(type, "$1"),
        };
    }

    [GeneratedRegex(@"^(tinyint|smallint|mediumint|int|bigint)\(\d+\)")]
    private static partial Regex IntegerDisplayWidth();

    private static string ComposeTableName(string schema, string table)
        => $"{IdentifierQuoter.QuoteForMySql(schema)}.{IdentifierQuoter.QuoteForMySql(table)}";

    internal static string ComposeSelectStructureCommandText()
    {
        StringBuilder commandText = new();
        commandText.Append(SelectAllSchemasWithInformation);
        return commandText.ToString();
    }

    internal static string ComposeCreateStructureCommandText(IReadOnlyCollection<Database> missingSchemas, IReadOnlyCollection<Database> missingTables)
    {
        StringBuilder commandText = new();

        foreach (var database in missingSchemas)
        {
            foreach (var schema in database.Schemas)
                commandText.AppendLine(ComposeCreateSchemaCommandText(schema));
        }

        foreach (var database in missingTables)
        {
            foreach (var schema in database.Schemas)
            {
                foreach (var table in schema.Tables)
                    commandText.AppendLine(ComposeCreateTableCommandText(schema, table));
            }
        }

        return commandText.ToString().TrimEnd();
    }

    internal static string ComposeCreateSchemaCommandText(Schema schema)
        => $"CREATE SCHEMA IF NOT EXISTS {IdentifierQuoter.QuoteForMySql(schema.Name)};";

    internal static string ComposeCreateTableCommandText(Schema schema, Table table)
    {
        StringBuilder command = new();
        var last = table.Columns.Count - 1;
        var i = 0;

        command.AppendFormat(CultureInfo.InvariantCulture, "CREATE TABLE {0} (", ComposeTableName(schema.Name, table.Name));
        command.AppendLine();

        foreach (var column in table.Columns)
        {
            command.Append(CultureInfo.InvariantCulture, $"{IdentifierQuoter.QuoteForMySql(column.Name)} {column.Type}");

            if (column.AutoIncrement)
                command.Append(" AUTO_INCREMENT");
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

    internal static string ComposeInsertCommand(TableInfo table, IReadOnlyCollection<ColumnInfo> columnInfos)
    {
        StringBuilder command = new();
        command.Append(CultureInfo.InvariantCulture, $"INSERT INTO {IdentifierQuoter.QuoteForMySql(table.Schema)}.{IdentifierQuoter.QuoteForMySql(table.Table)} (");
        command.AppendJoin(',', columnInfos.Select(c => IdentifierQuoter.QuoteForMySql(c.Name)));
        command.Append(") VALUES (");
        command.AppendJoin(',', columnInfos.Select(c => $"@{c.Name}"));
        command.Append(");");

        return command.ToString();
    }
}
