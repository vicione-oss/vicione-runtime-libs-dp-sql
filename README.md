# DataPort SQL databases

| Provider      | Incoming | Outgoing | free JSON structure |
|---------------|:--------:|:--------:|:-------------------:|
| MySql/MariaDB |    ❌    |    ✔️    |         ✔️          |
| PostgreSQL    |    ❌    |    ✔️    |         ✔️          |
| SQLite        |    ❌    |    ✔️    |         ✔️          |
| SQL-Server    |    ❌    |    ✔️    |         ✔️          |

## Node properties

| Name          | Typ    |
|---------------|--------|
| PrimaryKey    | `bool` |
| NotNull       | `bool` |
| AutoIncrement | `bool` |

## Writing behaviour

- **One row per engine cycle.** A table gets a new row for every cycle in which one of its columns changed. With the `OnChange` transfer mode a row only carries the columns that changed; every other column is `NULL`. There is no row that holds the current state.
- **A row of default values per deploy.** Every deploy publishes the default of each output once (`0`, `0001-01-01`, empty string), and that becomes a row. On a table with a primary key the second deploy therefore writes a duplicate key, and that row is dropped.
- **No timestamp column.** The DataPort does not add one. A column it does not know, such as `DEFAULT now()` or an identity column added by hand, counts as a breaking change unless `DisableStructureValidation` is set.
- **Rejected rows are dropped.** When the database rejects a row because of its values (duplicate key, `NULL` in a `NOT NULL` column, value out of range, `NaN` or `Infinity` on MySQL and MariaDB, `NaN` on SQLite, a string longer than 64 KB in a MySQL or MariaDB `text` column) the row is dropped, logged at `Error` with the table name and counted in the `vicione.dataport.sql.rows_dropped` counter of the `ViciOne.Suite.DataPort.Sql` meter. The other rows are written. On any other error, such as a lost connection, a timeout, a lock, a missing table or a missing permission, the rows stay queued and are written once the database accepts them again.
- **JSON columns** hold only the members that changed in that cycle, plus a `$meta` object with the .NET type names of the values.

### Value ranges

| Value | MySQL / MariaDB | PostgreSQL | SQLite |
|---|---|---|---|
| `UInt64` | `bigint unsigned` | `bigint`, values above `Int64.MaxValue` are rejected | `INTEGER`, values above `Int64.MaxValue` are rejected |
| `DateTime` | `datetime(6)`, microseconds | `timestamp`, microseconds, `DateTime.MinValue` is stored as `0001-01-01 00:00:00.000001` and `DateTime.MaxValue` as `9999-12-31 23:59:59.999999` | text, 100 ns |
| `Double` | finite values | all values | all values except `NaN` |
| `String` | `text`, up to 64 KB | `text`, no NUL character | `TEXT`, no NUL character |

MySQL and MariaDB tables created by an earlier version keep their `datetime` columns, which store whole seconds.
