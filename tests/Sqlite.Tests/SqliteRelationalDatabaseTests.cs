using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteRelationalDatabase_IsRowError
{
    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    public void Accepts_too_big_constraint_and_mismatch_errors(int errorCode)
        => SqliteRelationalDatabase.IsRowError(new SqliteException("rejected", errorCode)).Should().BeTrue();

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(13)]
    public void Rejects_busy_locked_readonly_and_full_errors(int errorCode)
        => SqliteRelationalDatabase.IsRowError(new SqliteException("failed", errorCode)).Should().BeFalse();
}
