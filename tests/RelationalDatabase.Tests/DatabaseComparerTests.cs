using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

[SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable",
    Justification = "Database und Co. können nicht für Xunit serialisierbar gemacht werden, da sie zur öffentlichen API gehören")]
public class DatabaseComparer_TryGetMissingTables
{
    [Theory]
    [MemberData(nameof(GetEqualStructure))]
    [MemberData(nameof(GetStructureWithMissingTable))]
    [MemberData(nameof(GetStructureWithMissingSchema))]
    [MemberData(nameof(GetEmptyStructure))]
    internal void Acceptance(List<Database> current, List<Database> expectedMissingTables, List<Database> expectedMissingDatabases, List<Database> expectedMissingSchemas, bool missing)
    {
        List<Database> expected =
        [
            new("db1",
            [
                new("public",
                [
                    new("users",
                    [
                        new("id", "INTEGER", true, true, true),
                        new("Name", "VARCHAR", false, false, false),
                    ]),
                    new("accessories",
                    [
                        new("id", "INTEGER", true, true, true),
                        new("user_id", "INTEGER", false, false, false),
                        new("Name", "VARCHAR", false, false, false),
                    ]),
                ]),
            ]),
        ];

        DatabaseComparer.TryGetMissingStructure(current, expected, out var missingTables, out var missingDatabases, out var missingSchemas).Should().Be(missing);
        missingTables.Should().BeEquivalentTo(expectedMissingTables);
        missingDatabases.Should().BeEquivalentTo(expectedMissingDatabases);
        missingSchemas.Should().BeEquivalentTo(expectedMissingSchemas);
    }

    public static TheoryData<List<Database>, List<Database>, List<Database>, List<Database>, bool> GetEqualStructure()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                new List<Database>(),
                new List<Database>(),
                false
            },
        };

    public static TheoryData<List<Database>, List<Database>, List<Database>, List<Database>, bool> GetStructureWithMissingTable()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                new List<Database>(),
                true
            },
        };

    public static TheoryData<List<Database>, List<Database>, List<Database>, List<Database>, bool> GetStructureWithMissingSchema()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1", []),
                },
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                true
            },
        };

    public static TheoryData<List<Database>, List<Database>, List<Database>, List<Database>, bool> GetEmptyStructure()
        => new()
        {
            {
                new List<Database>(),
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                true
            },
        };
}

[SuppressMessage("Usage", "xUnit1045:Avoid using TheoryData type arguments that might not be serializable",
    Justification = "Database und Co. können nicht für Xunit serialisierbar gemacht werden, da sie zur öffentlichen API gehören")]
public class DatabaseComparer_ExistingTablesAreUnequal
{
    [Theory]
    [MemberData(nameof(GetEqualStructure))]
    [MemberData(nameof(GetUnequalStructure))]
    [MemberData(nameof(GetStructureWithAdditionalTables))]
    [MemberData(nameof(GetStructureWithAdditionalColumn))]
    [MemberData(nameof(GetStructureWithLessColumns))]
    [MemberData(nameof(GetStructureWithLessTables))]
    internal void Acceptance(List<Database> current, List<Database> expectedDiffs, bool isUnequal)
    {
        List<Database> expected =
        [
            new("db1",
            [
                new("public",
                [
                    new("users",
                    [
                        new("id", "INTEGER", true, true, true),
                        new("Name", "VARCHAR", false, false, false),
                    ]),
                    new("accessories",
                    [
                        new("id", "INTEGER", true, true, true),
                        new("user_id", "INTEGER", false, false, false),
                        new("Name", "VARCHAR", false, false, false),
                    ]),
                ]),
            ]),
        ];

        DatabaseComparer.ExistingTablesAreUnequal(current, expected, out var diffs).Should().Be(isUnequal);
        diffs.Should().BeEquivalentTo(expectedDiffs);
    }

    public static TheoryData<List<Database>, List<Database>, bool> GetEqualStructure()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                false
            },
        };

    public static TheoryData<List<Database>, List<Database>, bool> GetUnequalStructure()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR(256)", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR(256)", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("Name", "VARCHAR(256)", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("Name", "VARCHAR(256)", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                true
            },
        };

    public static TheoryData<List<Database>, List<Database>, bool> GetStructureWithAdditionalTables()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                            new("additionals",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                false
            },
        };

    public static TheoryData<List<Database>, List<Database>, bool> GetStructureWithAdditionalColumn()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                                new("Age", "INTEGER", false, false, false),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("Age", "INTEGER", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                true
            },
        };

    public static TheoryData<List<Database>, List<Database>, bool> GetStructureWithLessColumns()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                            ]),
                            new("accessories",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("user_id", "INTEGER", false, false, false),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                false
            },
        };

    public static TheoryData<List<Database>, List<Database>, bool> GetStructureWithLessTables()
        => new()
        {
            {
                new List<Database>()
                {
                    new("db1",
                    [
                        new("public",
                        [
                            new("users",
                            [
                                new("id", "INTEGER", true, true, true),
                                new("Name", "VARCHAR", false, false, false),
                            ]),
                        ]),
                    ]),
                },
                new List<Database>(),
                false
            },
        };
}
