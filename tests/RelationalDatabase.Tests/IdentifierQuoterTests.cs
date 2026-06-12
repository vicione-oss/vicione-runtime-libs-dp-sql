using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class IdentifierQuoter_QuoteForSqlServer
{
    [Theory]
    [InlineData("users", "[users]")]
    [InlineData("my table", "[my table]")]
    [InlineData("has]bracket", "[has]]bracket]")]
    [InlineData("a]]b", "[a]]]]b]")]
    [InlineData("SELECT", "[SELECT]")]
    public void Returns_correctly_quoted_identifier(string name, string expected)
        => IdentifierQuoter.QuoteForSqlServer(name).Should().Be(expected);

    /// <summary>
    /// Verifies that identifiers containing SQL injection payloads are fully neutralized.
    /// The closing bracket ] is the only breakout character in T-SQL bracket identifiers;
    /// escaping every ] as ]] prevents any premature closure of the identifier.
    /// </summary>
    [Theory]
    [InlineData("]; DROP TABLE users; --", "[]]; DROP TABLE users; --]")]
    [InlineData("a]; DROP TABLE users; --", "[a]]; DROP TABLE users; --]")]
    [InlineData("x]] EXEC xp_cmdshell('y')", "[x]]]] EXEC xp_cmdshell('y')]")]
    [InlineData("'; DROP TABLE users; --", "['; DROP TABLE users; --]")]
    [InlineData("a' OR '1'='1", "[a' OR '1'='1]")]
    public void Neutralizes_sql_injection_payload(string payload, string expected)
        => IdentifierQuoter.QuoteForSqlServer(payload).Should().Be(expected);
}

public class IdentifierQuoter_QuoteForAnsiSql
{
    [Theory]
    [InlineData("users", "\"users\"")]
    [InlineData("my table", "\"my table\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("a\"\"b", "\"a\"\"\"\"b\"")]
    [InlineData("SELECT", "\"SELECT\"")]
    public void Returns_correctly_quoted_identifier(string name, string expected)
        => IdentifierQuoter.QuoteForAnsiSql(name).Should().Be(expected);

    /// <summary>
    /// Verifies that identifiers containing SQL injection payloads are fully neutralized.
    /// The double-quote " is the only breakout character in ANSI-quoted identifiers;
    /// escaping every " as "" prevents any premature closure of the identifier.
    /// </summary>
    [Theory]
    [InlineData("\"; DROP TABLE users; --", "\"\"\"; DROP TABLE users; --\"")]
    [InlineData("a\"; DROP TABLE users; --", "\"a\"\"; DROP TABLE users; --\"")]
    [InlineData("x\"\" UNION SELECT 1; --", "\"x\"\"\"\" UNION SELECT 1; --\"")]
    [InlineData("'; DROP TABLE users; --", "\"'; DROP TABLE users; --\"")]
    [InlineData("a' OR '1'='1", "\"a' OR '1'='1\"")]
    public void Neutralizes_sql_injection_payload(string payload, string expected)
        => IdentifierQuoter.QuoteForAnsiSql(payload).Should().Be(expected);
}

public class IdentifierQuoter_QuoteForMySql
{
    [Theory]
    [InlineData("users", "`users`")]
    [InlineData("my table", "`my table`")]
    [InlineData("has`backtick", "`has``backtick`")]
    [InlineData("a``b", "`a````b`")]
    [InlineData("SELECT", "`SELECT`")]
    public void Returns_correctly_quoted_identifier(string name, string expected)
        => IdentifierQuoter.QuoteForMySql(name).Should().Be(expected);

    /// <summary>
    /// Verifies that identifiers containing SQL injection payloads are fully neutralized.
    /// The backtick ` is the only breakout character in MySQL backtick-quoted identifiers;
    /// escaping every ` as `` prevents any premature closure of the identifier.
    /// </summary>
    [Theory]
    [InlineData("`; DROP TABLE users; --", "```; DROP TABLE users; --`")]
    [InlineData("a`; DROP TABLE users; --", "`a``; DROP TABLE users; --`")]
    [InlineData("x`` UNION SELECT 1; --", "`x```` UNION SELECT 1; --`")]
    [InlineData("'; DROP TABLE users; --", "`'; DROP TABLE users; --`")]
    [InlineData("a' OR '1'='1", "`a' OR '1'='1`")]
    public void Neutralizes_sql_injection_payload(string payload, string expected)
        => IdentifierQuoter.QuoteForMySql(payload).Should().Be(expected);
}

/// <summary>
/// SQLite uses ANSI SQL double-quote delimited identifiers, identical to PostgreSQL.
/// These tests confirm SQLite identifiers go through the same quoting and are equally injection-safe.
/// </summary>
public class IdentifierQuoter_QuoteForSqlite
{
    [Theory]
    [InlineData("users", "\"users\"")]
    [InlineData("my table", "\"my table\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("a\"\"b", "\"a\"\"\"\"b\"")]
    [InlineData("SELECT", "\"SELECT\"")]
    public void Returns_correctly_quoted_identifier(string name, string expected)
        => IdentifierQuoter.QuoteForAnsiSql(name).Should().Be(expected);

    [Theory]
    [InlineData("\"; DROP TABLE users; --", "\"\"\"; DROP TABLE users; --\"")]
    [InlineData("a\"; DROP TABLE users; --", "\"a\"\"; DROP TABLE users; --\"")]
    [InlineData("x\"\" UNION SELECT 1; --", "\"x\"\"\"\" UNION SELECT 1; --\"")]
    [InlineData("'; DROP TABLE users; --", "\"'; DROP TABLE users; --\"")]
    [InlineData("a' OR '1'='1", "\"a' OR '1'='1\"")]
    public void Neutralizes_sql_injection_payload(string payload, string expected)
        => IdentifierQuoter.QuoteForAnsiSql(payload).Should().Be(expected);
}
