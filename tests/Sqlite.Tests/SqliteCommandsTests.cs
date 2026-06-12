using System;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class SqliteCommands_ComposeMissingStructureCommandText
{
    [Fact]
    public void Acceptance()
    {
        var databases = new Database[]
        {
            new("default",
            [
                new("default",
                [
                    new("users",
                    [
                        new("id", "INTEGER", true, false, false),
                        new("name", "TEXT", false, false, false),
                    ]),
                    new("ranks",
                    [
                        new("id", "INTEGER", true, true, false),
                        new("user_id", "INTEGER", false, false, false),
                        new("name", "TEXT", false, false, false),
                    ]),
                ]),
            ]),
        };

        SqliteCommands.ComposeMissingStructureCommandText(databases).Should().Be("""
        CREATE TABLE "users" (
        "id" INTEGER PRIMARY KEY,
        "name" TEXT);
        CREATE TABLE "ranks" (
        "id" INTEGER PRIMARY KEY NOT NULL,
        "user_id" INTEGER,
        "name" TEXT);
        """.ReplaceLineEndings());
    }
}

public class SqliteCommands_ComposeInsertRowsCommandText
{
    [Fact]
    public void Acceptance()
    {
        TableInfo table = new("default", "default", "main");
        var columns = new ColumnInfo[]
        {
            new(table, "id", false, false, typeof(Guid)),
            new(table, "name", false, false, typeof(string)),
            new(table, "age", false, false, typeof(int)),
        };

        SqliteCommands.ComposeInsertRowsCommandText(table.Table, columns).Should().Be(
            "INSERT INTO \"main\" (\"id\",\"name\",\"age\") VALUES (@id,@name,@age);");
    }
}
