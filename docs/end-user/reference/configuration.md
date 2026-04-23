# Configuration Reference

Applies to: SchemaQuench, SchemaTongs, DataTongs — SQL Server, PostgreSQL, and MySQL.

---

Every SchemaSmith CLI tool shares the same configuration spine -- one consistent system for settings files, environment variables, and command-line switches. Learn it once and it works the same way whether you're casting schemas, quenching databases, or extracting data, and whether your target is SQL Server, PostgreSQL, or MySQL. Tool-specific settings live on each tool's own reference page; this page covers the shared foundation.

The **platform** each operation runs against is not a CLI switch or an environment variable -- it's a property of the schema package itself. `Product.json` declares `Platform` (`SqlServer`, `PostgreSql`, or `MySql`), and the tools adapt their behavior accordingly. One tool, three engines, same muscle memory.

---

## CLI Switch Format

SchemaSmith is flexible about how you pass switches -- pick whichever style feels natural. All switches accept either a double-dash (`--`) or forward-slash (`/`) prefix. Separate the switch name from its value with `:` or `=`. A single leading dash (`-`) also works.

```
--switch:value
--switch=value
/switch:value
-switch:value
```

Switch names are **case-insensitive** -- `--logpath`, `--LogPath`, and `--LOGPATH` all do the same thing.

Values that contain spaces must be quoted:

```
--ConfigFile:"C:\configs\my config.json"
/LogPath:"C:\My Logs\SchemaSmith"
```

Flags that take no value (like `--version` or `--help`) are specified without a separator.

---

## Common Switches

Every SchemaSmith CLI tool recognizes these switches. They're processed before any configuration is loaded.

| Switch | Aliases | Description |
|---|---|---|
| `--version` | `-v`, `--ver` | Print the tool name, edition, and version number, then exit. |
| `--help` | `-h`, `-?` | Print the available command-line switches, then exit. |
| `--ConfigFile:<path>` | | Path to the settings file. Overrides the default `<ToolName>.settings.json`. |
| `--LogPath:<path>` | | Directory for log files and backup subdirectories. Defaults to the tool's executable directory. |
| `--ConnectionString:<connstr>` | | Full ADO.NET / Npgsql / MySqlConnector connection string appropriate to the target platform. When provided, this bypasses all individual connection settings (`Server`, `Port`, `User`, `Password`, `ConnectionProperties`). |

### Examples

```bash
# Deploy a schema package with a custom config and log directory
SchemaQuench --ConfigFile:production.json --LogPath:C:\Logs

# Extract a schema using a specific config
SchemaTongs --ConfigFile:extract-config.json

# Export data merge scripts, overriding the connection entirely (SQL Server)
DataTongs --ConnectionString:"Data Source=myserver;Initial Catalog=mydb;User ID=sa;Password=secret;TrustServerCertificate=True;"

# Same thing, PostgreSQL
DataTongs --ConnectionString:"Host=myserver;Port=5432;Database=mydb;Username=deploy;Password=secret;"

# Same thing, MySQL
DataTongs --ConnectionString:"Server=myserver;Port=3306;Database=mydb;User=deploy;Password=secret;"

# Check which version is installed
SchemaQuench --version

# See available switches
SchemaTongs --help
```

---

## Configuration Hierarchy

SchemaSmith layers configuration so you can set sensible defaults in a file and override just the pieces that change per environment. Later sources override earlier ones. The full chain, from lowest to highest priority:

1. **Settings file** -- `<ToolName>.settings.json`
2. **User secrets** -- .NET user secrets (debug builds only, not present in release builds)
3. **Environment variables** -- prefixed with `SmithySettings_`
4. **CLI switches** -- `--ConnectionString`, `--ConfigFile`, `--LogPath`

This means a value set in the settings file can be overridden by an environment variable, and a CLI switch always wins.

### Override example

Suppose your `SchemaQuench.settings.json` sets the server:

```json
{
    "Target": {
        "Server": "dev-server"
    }
}
```

You can override just the server for a single run using an environment variable:

```bash
export SmithySettings_Target__Server=staging-server
SchemaQuench
```

Or override the entire connection from the command line:

```bash
SchemaQuench --ConnectionString:"Host=prod-server;Database=mydb;Username=deploy;Password=s3cret;"
```

The `--ConnectionString` switch bypasses all individual connection settings -- `Server`, `Port`, `User`, `Password`, and `ConnectionProperties` are all ignored when a full connection string is provided.

---

## Settings Files

Each tool looks for its own settings file by name:

| Tool | Default settings file |
|---|---|
| SchemaQuench | `SchemaQuench.settings.json` |
| SchemaTongs | `SchemaTongs.settings.json` |
| DataTongs | `DataTongs.settings.json` |

The tool searches for the file in two locations, in order:

1. The **current working directory** (where you run the command)
2. The **tool's executable directory** (where the binary lives)

If the file is found in the current directory, that copy is used. If not, the tool falls back to the executable directory. If neither location has the file, the tool starts with an empty configuration (any required values must come from environment variables or CLI switches).

To use a different file entirely, pass the `--ConfigFile` switch:

```bash
SchemaQuench --ConfigFile:C:\configs\production.json
```

The path can be absolute or relative to the current working directory.

---

## Environment Variables

Environment variables give you a clean way to inject configuration without touching files on disk -- exactly what you need in CI/CD pipelines and containers. All three tools read environment variables prefixed with `SmithySettings_`. The prefix is stripped, and double underscores (`__`) map to hierarchy separators in the configuration structure.

### Mapping rules

| Environment variable | Maps to config key | JSON equivalent |
|---|---|---|
| `SmithySettings_Target__Server` | `Target:Server` | `{ "Target": { "Server": "..." } }` |
| `SmithySettings_Target__Port` | `Target:Port` | `{ "Target": { "Port": "..." } }` |
| `SmithySettings_Target__User` | `Target:User` | `{ "Target": { "User": "..." } }` |
| `SmithySettings_Target__Password` | `Target:Password` | `{ "Target": { "Password": "..." } }` |
| `SmithySettings_Target__ConnectionProperties__TrustServerCertificate` | `Target:ConnectionProperties:TrustServerCertificate` | `{ "Target": { "ConnectionProperties": { "TrustServerCertificate": "..." } } }` |
| `SmithySettings_SchemaPackagePath` | `SchemaPackagePath` | `{ "SchemaPackagePath": "..." }` |
| `SmithySettings_WhatIfONLY` | `WhatIfONLY` | `{ "WhatIfONLY": "..." }` |

SchemaTongs and DataTongs use `Source` instead of `Target` for their connection section, so the equivalent variables start with `SmithySettings_Source__`:

```bash
# SchemaQuench connection
export SmithySettings_Target__Server=myserver
export SmithySettings_Target__Password=s3cret

# SchemaTongs / DataTongs connection
export SmithySettings_Source__Server=myserver
export SmithySettings_Source__Password=s3cret
```

Environment variables are especially useful in CI/CD pipelines and containers where you don't want secrets in files on disk.

---

## Connection Configuration

Each tool has one connection section. SchemaQuench uses a `Target` section (it writes to the server), while SchemaTongs and DataTongs use a `Source` section (they read from the server). The structure is the same either way, and the same keys work for every supported platform -- the adapter under the hood routes the call to the right client library based on the product's declared platform.

### Individual connection settings

```json
{
    "Target": {
        "Server": "myserver",
        "Port": "",
        "User": "deploy",
        "Password": "s3cret",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    }
}
```

| Key | Purpose |
|---|---|
| `Server` | Database server hostname or IP address. |
| `Port` | TCP port. Leave blank for the platform default (SQL Server `1433`, PostgreSQL `5432`, MySQL `3306`). |
| `User` | Login username. |
| `Password` | Login password. |
| `Database` | Database name. Used by SchemaTongs and DataTongs. SchemaQuench reads its target databases from the schema package instead. |
| `ConnectionProperties` | Dictionary of additional connection string properties. Each key-value pair is appended to the built connection string. |

**Windows authentication (SQL Server only):** Leave both `User` and `Password` blank. The tool will connect using the identity of the process. PostgreSQL and MySQL require explicit credentials.

**Platform-specific connection properties:**

- *SQL Server* — `TrustServerCertificate`, `Encrypt`, `ApplicationIntent`, etc.
- *PostgreSQL* — `SslMode`, `Pooling`, `Timeout`, etc. (Npgsql keys)
- *MySQL* — `SslMode`, `ConnectionTimeout`, `AllowPublicKeyRetrieval`, etc. (MySqlConnector keys)

Whatever you put in `ConnectionProperties` is appended to the connection string for that platform's client library. Consult the corresponding driver documentation for the exhaustive list.

### Full connection string override

Instead of individual settings, you can provide a complete connection string for the target platform:

```bash
# SQL Server
SchemaQuench --ConnectionString:"Data Source=myserver;Initial Catalog=mydb;User ID=sa;Password=s3cret;TrustServerCertificate=True;"

# PostgreSQL
SchemaQuench --ConnectionString:"Host=myserver;Port=5432;Database=mydb;Username=deploy;Password=s3cret;"

# MySQL
SchemaQuench --ConnectionString:"Server=myserver;Port=3306;Database=mydb;User=deploy;Password=s3cret;"
```

When `--ConnectionString` is provided, all individual connection settings (`Server`, `Port`, `User`, `Password`, `ConnectionProperties`) are bypassed.

---

## Password Masking

Your credentials stay out of the logs. When a tool logs its active configuration at startup, it masks any value whose key contains `Password` or `Pwd` (case-insensitive). The masked value appears as `**********` in both the progress log and console output.

All other configuration values are logged as-is. This lets you audit the active configuration from the log without exposing credentials.

Example log output:

```
Configuration:
    Server: myserver
    Port: 5432
    User: deploy
    Password: **********
    ConnectionProperties:
      SslMode: Prefer
```

---

## Logging

Good logs are the difference between a quick diagnosis and a long night. SchemaSmith gives you detailed, automatic logging out of the box.

### Framework

All CLI tools use [Apache Log4Net](https://logging.apache.org/log4net/). Each tool ships with an embedded Log4Net configuration that is loaded automatically at startup -- there's nothing to configure.

### Log files

Each tool writes two log files per run:

| Tool | Progress log | Error log |
|---|---|---|
| SchemaQuench | `SchemaQuench - Progress.log` | `SchemaQuench - Errors.log` |
| SchemaTongs | `SchemaTongs - Progress.log` | `SchemaTongs - Errors.log` |
| DataTongs | `DataTongs - Progress.log` | `DataTongs - Errors.log` |

The **progress log** receives all informational output: the startup banner, active configuration, per-object progress, and completion status. Everything written to the progress log also appears on the console in real time.

The **error log** receives only error-level entries (such as SQL execution errors) and does **not** echo to the console.

Both log files are overwritten at the start of each run. Previous runs are preserved through the backup rotation described below.

### Console output

The console is a live mirror of the progress log, not a separate channel. Watching the console while a quench runs lets you follow startup, per-object progress, and completion in real time without tailing a file. Log4Net colorizes the console stream by level -- informational entries in green, warnings in yellow, errors in red -- so trouble catches your eye the moment it appears.

Errors flow differently. They land in the error log only, so the console stays focused on forward progress while the quench is in flight. When a run fails, read the error log for the exception and the progress log for the context around where it happened -- the two files run in lockstep and share timestamps.

> **Tip:** CI agents that capture stdout get the progress stream for free. If your pipeline step only saves stdout, you still have a readable transcript of what the tool did; archive the progress and error files for the full picture.

### Log file location

By default, logs are written to the tool's executable directory. Override this with `--LogPath`:

```bash
SchemaQuench --LogPath:C:\Logs
SchemaTongs --LogPath:/var/log/schemasmith
DataTongs --LogPath:D:\BuildLogs
```

### Startup configuration dump

Immediately after loading configuration, every tool logs its complete active configuration to the progress log. This includes the tool name, edition, and version number, followed by every configuration key and its value (with passwords masked). This makes it straightforward to verify what settings were in effect for any given run.

### Log backup rotation

When a tool finishes (whether successfully or after an error), it backs up its log files before the process exits:

1. Determines the log directory (`--LogPath` value, or the executable directory if not specified).
2. Creates a numbered subdirectory: `<ToolName>.0001`. If that directory already exists, it increments: `.0002`, `.0003`, and so on.
3. Copies all files matching `<ToolName> - *.log` into the new subdirectory.

The base log files in the log directory are **not deleted** after backup. Each run overwrites the base files and writes a copy into a new numbered subdirectory. This preserves the history of every run while keeping the base files current with the latest.

Example after three SchemaQuench runs:

```
C:\Tools\
    SchemaQuench - Progress.log      (latest run)
    SchemaQuench - Errors.log        (latest run)
    SchemaQuench.0001\               (first run backup)
    SchemaQuench.0002\               (second run backup)
    SchemaQuench.0003\               (third run backup)
```

### Debug SQL files

When SchemaQuench runs one of its generated procedures against your target database, it dumps the exact SQL it sent to a companion `.sql` file next to the tool executable. If the procedure throws, the error log points you at the file by name. Open it in your query tool of choice, re-run the SQL by hand, and reproduce or narrow the problem without guessing what SchemaSmith actually executed.

Generated procedures cover missing tables and columns, modified tables, indexes, foreign keys, materialized views, indexed views, and the table-JSON parse step. Debug files follow the pattern `SchemaQuench - <operation> <server>.<database>.sql`:

```
SchemaQuench - Quench Missing Tables And Columns prod-db.NorthwindClone.sql
SchemaQuench - Quench Modified Tables prod-db.NorthwindClone.sql
SchemaQuench - Quench Indexes prod-db.NorthwindClone.sql
SchemaQuench - Quench Foreign Keys prod-db.NorthwindClone.sql
SchemaQuench - Quench Materialized Views prod-db.NorthwindClone.sql
SchemaQuench - Quench Indexed Views prod-db.NorthwindClone.sql
SchemaQuench - Parse Table Json prod-db.NorthwindClone.sql
```

Each run overwrites the debug files for the operations it actually performed. Operations that don't apply to your platform (for example, `Indexed Views` on PostgreSQL or `Materialized Views` on MySQL) produce no file. Debug files always land next to the tool executable -- `--LogPath` controls the progress and error logs, not debug SQL.

> **Note:** Debug files only cover SchemaSmith's own generated procedures. The user-authored scripts in your package (`Before/`, `After/`, migration scripts, programmable objects) already live on disk at their original paths -- when one of them fails, the error log points at the file you authored.

### Engine notices

SchemaSmith surfaces the database engine's informational output -- notices, prints, and server-side status messages -- into the progress log so you can see what the engine is telling you. The wiring differs per platform because each driver exposes that stream differently:

- **SQL Server** -- `PRINT` output and severity-10-or-lower errors arrive through the `InfoMessage` event. By default SchemaSmith promotes only severity-above-10 errors and `RAISERROR ... WITH STATE 100` notifications to the progress log; set `VerboseLogging: true` to include every `PRINT` and informational message.
- **PostgreSQL** -- `RAISE NOTICE` and `RAISE WARNING` output arrives through the Npgsql `Notice` event and lands in the progress log. SchemaSmith filters out the `"... does not exist, skipping"` and `"... already exists, skipping"` notices that `DROP ... IF EXISTS` and `CREATE ... IF NOT EXISTS` produce during normal runs, so your log stays readable.
- **MySQL** -- the MySQL connector doesn't fire info-message events for long-running stored procedures, so SchemaSmith uses a table-based status channel. A `SchemaSmith_StatusMessages` table in the target database (created automatically during kindling) holds per-session progress rows; the generated quench procedures `INSERT` into it as they work, and a background poller on a separate connection reads the new rows every 200ms and writes them to the progress log. `SessionId` is scoped to `CONNECTION_ID()` so concurrent runs don't cross-talk, and the monitor deletes its rows on shutdown. There's no `VerboseLogging` dial on MySQL -- what you see is whatever the procedures chose to publish.

`VerboseLogging` is a SchemaQuench setting and applies only to SQL Server's `InfoMessage` stream. The PostgreSQL and MySQL paths already behave the way `VerboseLogging: true` behaves on SQL Server -- SchemaSmith surfaces every engine-side notice (PostgreSQL) or procedure-emitted status message (MySQL) by default.

---

## Exit Codes

| Code | Condition | Recommended action |
|---|---|---|
| `0` | Normal completion | None -- the operation succeeded. |
| `2` | One or more database quenches failed (SchemaQuench only) | Check the progress and error logs for details on which databases failed and why. Fix the failing scripts and re-run. |
| `3` | Unhandled exception | An unexpected error occurred. The exception is logged to both the progress and error logs before exit. Report the error with the log contents if the cause isn't obvious. |
| `4` | Log backup failure | The tool completed its main work but couldn't back up the log files. Check directory permissions and disk space in the log directory. The base log files may still be readable even though the backup failed. |

---

## Quick Reference

```bash
# Run with defaults (settings file in current directory or tool directory)
SchemaQuench

# Custom config file
SchemaQuench --ConfigFile:staging.json

# Custom log directory
SchemaQuench --LogPath:/var/log/quench

# Override connection via environment
export SmithySettings_Target__Server=prod-db
export SmithySettings_Target__User=deploy
export SmithySettings_Target__Password=s3cret
SchemaQuench

# Override connection via CLI (platform-appropriate connection string)
SchemaQuench --ConnectionString:"Host=prod-db;Database=mydb;Username=deploy;Password=s3cret;"
```

---

## Related Documentation

- [SchemaQuench Reference](schemaquench.md) -- Deployment engine settings and behavior
- [SchemaTongs Reference](schematongs.md) -- Schema extraction settings
- [DataTongs Reference](datatongs.md) -- Data extraction settings
- [Script Tokens Reference](script-tokens.md) -- Token replacement via ScriptTokens config
- [CI/CD Integration](../guide/07-cicd-integration.md#configuration-via-environment-variables) -- Practical guide to environment variable configuration in pipelines
