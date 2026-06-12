using System.Collections.Generic;
using System.Linq;

namespace ViciOne.Suite.DataPort;

public static class DatabaseComparer
{
    public static bool TryGetMissingStructure(IReadOnlyCollection<Database> current, IReadOnlyCollection<Database> expected, out List<Database> missingTables, out List<Database> missingDatabases, out List<Database> missingSchemas)
    {
        missingTables = [];
        missingDatabases = [];
        missingSchemas = [];

        foreach (var expectedDatabase in expected)
        {
            var currentDatabase = current.FirstOrDefault(d => d.Name == expectedDatabase.Name);

            if (currentDatabase is null)
            {
                missingTables.Add(expectedDatabase);
                missingDatabases.Add(expectedDatabase);
                missingSchemas.Add(expectedDatabase);
                continue;
            }

            List<Schema> tableSchemas = [];
            List<Schema> schemaSchemas = [];

            foreach (var expectedSchema in expectedDatabase.Schemas)
            {
                var currentSchema = currentDatabase.Schemas.FirstOrDefault(d => d.Name == expectedSchema.Name);

                if (currentSchema is null)
                {
                    tableSchemas.Add(expectedSchema);
                    schemaSchemas.Add(expectedSchema);
                    continue;
                }

                List<Table> tables = [];

                foreach (var expectedTable in expectedSchema.Tables)
                {
                    var currentTable = currentSchema.Tables.FirstOrDefault(d => d.Name == expectedTable.Name);

                    if (currentTable is null)
                    {
                        tables.Add(expectedTable);
                        continue;
                    }
                }

                if (tables.Count > 0)
                    tableSchemas.Add(expectedSchema with { Tables = tables, });
            }

            if (schemaSchemas.Count > 0)
                missingSchemas.Add(expectedDatabase with { Schemas = schemaSchemas, });

            if (tableSchemas.Count > 0)
                missingTables.Add(expectedDatabase with { Schemas = tableSchemas, });
        }

        return missingTables.Count > 0;
    }

    public static bool ExistingTablesAreUnequal(IReadOnlyCollection<Database> current, IReadOnlyCollection<Database> expected, out List<Database> differedTables)
    {
        differedTables = [];

        foreach (var expectedDatabase in expected)
        {
            var currentDatabase = current.FirstOrDefault(d => d.Name == expectedDatabase.Name);

            if (currentDatabase is null)
                continue;

            List<Schema> schemas = [];

            foreach (var expectedSchema in expectedDatabase.Schemas)
            {
                var currentSchema = currentDatabase.Schemas.FirstOrDefault(d => d.Name == expectedSchema.Name);

                if (currentSchema is null)
                    continue;

                List<Table> tables = [];

                foreach (var expectedTable in expectedSchema.Tables)
                {
                    var currentTable = currentSchema.Tables.FirstOrDefault(d => d.Name == expectedTable.Name);

                    if (currentTable is null)
                        continue;

                    var differedColumns = currentTable.Columns.Except(expectedTable.Columns).ToList();

                    if (differedColumns.Count > 0)
                        tables.Add(expectedTable with { Columns = differedColumns, });
                }

                if (tables.Count > 0)
                    schemas.Add(expectedSchema with { Tables = tables, });
            }

            if (schemas.Count > 0)
                differedTables.Add(expectedDatabase with { Schemas = schemas, });
        }

        return differedTables.Count > 0;
    }
}
