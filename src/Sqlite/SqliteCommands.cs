using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ViciOne.Suite.DataPort;

internal static class SqliteCommands
{
    internal const string SelectAllTablesWithColumns =
        "SELECT tables.name, columns.name, columns.type, columns.pk, columns.\"notnull\" " +
        "FROM sqlite_master AS tables, pragma_table_info(tables.name) AS columns " +
        "WHERE tables.type ='table' AND tables.name NOT LIKE 'sqlite_%'";

    internal static TablesWithColumnsResult ReadTablesWithColumnsResult(DbDataReader reader)
        => new(
            reader.GetFieldValue<string>(0),
            reader.GetFieldValue<string>(1),
            reader.GetFieldValue<string>(2),
            reader.GetFieldValue<bool>(3),
            reader.GetFieldValue<bool>(4));

    internal static string ComposeMissingStructureCommandText(IReadOnlyCollection<Database> databases)
    {
        var tables = databases.Single().Schemas.Single().Tables;
        StringBuilder commandText = new();

        foreach (var table in tables)
            commandText.AppendLine(ComposeCreateTableCommandText(table));

        return commandText.ToString().TrimEnd();
    }

    private static string ComposeCreateTableCommandText(Table table)
    {
        StringBuilder command = new();
        command.Append(CultureInfo.InvariantCulture, $"CREATE TABLE {IdentifierQuoter.QuoteForAnsiSql(table.Name)} (");
        command.AppendLine();

        var last = table.Columns.Count - 1;
        var i = 0;
        foreach (var column in table.Columns)
        {
            command.Append(CultureInfo.InvariantCulture, $"{IdentifierQuoter.QuoteForAnsiSql(column.Name)} {column.Type}");

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

    internal static string ComposeInsertRowsCommandText(string table, IEnumerable<ColumnInfo> columnInfos)
    {
        StringBuilder commandText = new();
        commandText.Append("INSERT INTO ");
        commandText.Append(IdentifierQuoter.QuoteForAnsiSql(table));
        commandText.Append(" (");
        commandText.AppendJoin(',', columnInfos.Select(c => IdentifierQuoter.QuoteForAnsiSql(c.Name)));
        commandText.Append(") VALUES (");
        commandText.AppendJoin(',', columnInfos.Select(c => $"@{c.Name}"));
        commandText.Append(");");

        return commandText.ToString();
    }
}
