using System;
using AwesomeAssertions;
using Npgsql;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class PostgreSqlRelationalDatabase_IsRowError
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.NotNullViolation)]
    [InlineData(PostgresErrorCodes.NumericValueOutOfRange)]
    [InlineData(PostgresErrorCodes.CharacterNotInRepertoire)]
    [InlineData(PostgresErrorCodes.DatetimeFieldOverflow)]
    public void Accepts_data_and_constraint_errors(string sqlState)
        => PostgreSqlRelationalDatabase.IsRowError(new PostgresException("rejected", "ERROR", "ERROR", sqlState)).Should().BeTrue();

    [Theory]
    [InlineData(PostgresErrorCodes.UndefinedTable)]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege)]
    [InlineData(PostgresErrorCodes.ReadOnlySqlTransaction)]
    [InlineData(PostgresErrorCodes.DiskFull)]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    [InlineData(PostgresErrorCodes.AdminShutdown)]
    public void Rejects_errors_that_do_not_belong_to_a_row(string sqlState)
        => PostgreSqlRelationalDatabase.IsRowError(new PostgresException("failed", "ERROR", "ERROR", sqlState)).Should().BeFalse();

    [Fact]
    public void Rejects_other_exceptions()
        => PostgreSqlRelationalDatabase.IsRowError(new TimeoutException()).Should().BeFalse();
}
