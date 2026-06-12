using System;

namespace ViciOne.Suite.DataPort;

public static class IdentifierQuoter
{
    public static string QuoteForSqlServer(string name)
        => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    public static string QuoteForAnsiSql(string name)
        => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public static string QuoteForMySql(string name)
        => "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";
}
