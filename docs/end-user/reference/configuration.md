# Configuration Reference

Applies to: SchemaQuench, SchemaTongs, DataTongs — SQL Server, PostgreSQL, and MySQL.

---

Every SchemaSmith CLI tool shares the same configuration spine -- one consistent system for settings files, environment variables, and command-line switches. Learn it once and it works the same way whether you're casting schemas, quenching databases, or extracting data, and whether your target is SQL Server, PostgreSQL, or MySQL. Tool-specific settings live on each tool's own reference page; this page covers the shared foundation.

The **platform** each operation runs against is not a CLI switch or an environment variable -- it's a property of the schema package itself. `Product.json` declares `Platform` (`SqlServer`, `PostgreSQL`, or `MySQL`), and the tools adapt their behavior accordingly. One tool, three engines, same muscle memory.

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
| `--version` | `-v`, `--ver` | Print the tool name and version number, then exit. |
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

## Sensitive value masking

Your credentials stay out of the logs. When a tool logs its active configuration at startup -- and when SchemaQuench logs its product and template script tokens -- it scrubs any value whose name matches a built-in sensitive-name set, so a log is safe to attach to a support ticket, paste into a CI artifact, or drop into a screenshot.

The default sensitive-name patterns (case-insensitive, substring match) are `Password`, `Pwd`, `Secret`, `ApiKey`, `Token`, `ConnectionString`, and `Credential`. A matched value renders as `***` while its name still prints, so you can confirm the setting exists without exposing it. An embedded `Password=` / `Pwd=` inside a connection-string value is stripped even when the surrounding setting or token is not sensitively named -- one leaked connection string is one too many.

All other values are logged as-is, so you can still audit the active configuration from the log.

Example log output:

```
Configuration:
    Server: myserver
    Port: 5432
    User: deploy
    Password: ***
    ConnectionProperties:
      SslMode: Prefer
```

### Tuning the scrubbing

An optional `LogHygiene` block in any tool's `*.settings.json` tunes the behavior. With no block present, the defaults above apply.

```jsonc
"LogHygiene": {
  // Suppress the token-logging section entirely -- one notice line, no token
  // names and no values. For products with hundreds of tokens. Default: true.
  "LogTokens": true,

  // Scrub these exact token names too, beyond the default patterns.
  "ScrubTokens": [ "Handshake", "TenantSeed" ],

  // Scrub names matching these extra patterns (contains-match; the * is optional).
  "ScrubPatterns": [ "*Salt*", "*PrivateKey*" ],

  // Opt a false positive back out -- log this name verbatim even though it
  // matches a default pattern (e.g. a column literally named "Token").
  "AllowTokens": [ "PublicToken" ]
}
```

When a token name appears in both `AllowTokens` and a scrub rule, `AllowTokens` wins and the value is logged verbatim -- but an embedded connection-string password is still stripped.

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

When a script fails, you see a short error summary on the console and in the progress log (red, so it's hard to miss): the failing script path and the engine's error message, along with a `Debug Script:` pointer when a generated procedure is the source. That's enough to know what failed and where to look. The **full detail** -- the exception line numbers and the complete SQL batches SchemaSmith submitted -- lands in the error log only, so the console and progress stream don't drown in multi-KB failed-batch text during a rough deployment. When a run fails, the progress log tells you *what* broke; the error log tells you *exactly what SQL was sent* when it broke.

> **Tip:** CI agents that capture stdout get the progress stream for free. If your pipeline step only saves stdout, you still have a readable transcript of successes and error summaries; archive the error log separately to keep the failed-batch detail.

### Log file location

By default, logs are written to the tool's executable directory. Override this with `--LogPath`:

```bash
SchemaQuench --LogPath:C:\Logs
SchemaTongs --LogPath:/var/log/schemasmith
DataTongs --LogPath:D:\BuildLogs
```

### Startup configuration dump

Immediately after loading configuration, every tool logs its complete active configuration to the progress log. This includes the tool name and version number, followed by every configuration key and its value (with passwords masked). This makes it straightforward to verify what settings were in effect for any given run.

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

### Failure artifacts

When any script fails during a SchemaQuench deployment -- a user-authored script, a generated table-quench procedure, or a data-delivery merge -- SchemaQuench writes the exact token-expanded SQL the server rejected to a re-runnable `.sql` artifact file. The progress log tells you where:

```
Unable to quench 'Before/01-seed-config.sql': Invalid column name 'Region'.
    Resolved SQL written to: C:\deploy\SchemaQuench - Failed 01-seed-config prod-db.TargetDB.sql
```

For generated procedures, the same file also appears in the `Debug Script:` log line when the procedure throws:

```
FAILED to quench: ...
Debug Script: 'C:\deploy\SchemaQuench - Quench Missing Tables And Columns prod-db.TargetDB.sql'
```

Both shapes point to the same kind of artifact: a `.sql` file with a comment header (server/database/schema, the failing script name, the error message), every batch the engine received, separated by `GO`, with the last-attempted batch marked `-- >>> FAILING BATCH (#N) >>>`. The failing-batch marker is a best-effort hint -- the engine marks the last batch it attempted, which is usually the one that caused the error.

**Artifacts are raw by default** -- all token values are already expanded to their real values, so you can open the file, connect to the target, and reproduce the failure immediately without any further substitution.

> **Note:** Artifacts land in the `ArtifactPath` directory (default: current working directory), not the log directory. This is intentional: raw artifacts may contain expanded sensitive values and should not be automatically swept into log archives or CI artifacts.

#### `ArtifactPath`

Directory where SchemaQuench writes resolved-SQL failure artifacts and generated-SQL debug files.

```json
{ "ArtifactPath": "C:\\deploy\\debug" }
```

| Default | Behavior |
|---|---|
| *(not set)* | Artifacts land in the current working directory (where SchemaQuench was launched). |
| A directory path | Artifacts land in the specified directory. Relative paths are resolved from the current working directory. |

Setting `ArtifactPath` is useful when you want artifacts in a consistent location regardless of where SchemaQuench is invoked -- a CI agent's workspace directory, for example, or a dedicated debug folder outside the log path. The directory is created automatically if it does not exist.

#### `ScrubArtifacts`

Controls whether sensitive values are redacted in failure artifacts before writing.

```json
{ "ScrubArtifacts": true }
```

| Value | Behavior |
|---|---|
| `false` (default) | Artifacts contain real expanded values. Re-runnable immediately -- open in a query tool and reproduce the failure without restoring secrets. |
| `true` | Sensitive token values (names matching `*Password*`, `*Secret*`, `*ApiKey*`, `*Token*`, etc., per `LogHygiene` rules) and inline connection-string passwords are redacted to `***`. Safe to attach to a CI artifact or support ticket. To reproduce, restore the real values first. |

Leave `ScrubArtifacts` off for local debugging -- raw artifacts are immediately re-runnable. Turn it on for CI environments or when attaching an artifact to a support ticket. The `LogHygiene` block governs which token names are considered sensitive; see [Sensitive value masking](#sensitive-value-masking) for the full set of rules.

For a step-by-step walkthrough of working a failed deployment from artifact to fix, see [Troubleshooting -- My deployment failed](../guide/12-troubleshooting.md#my-deployment-failed).

### Debug SQL files

When SchemaQuench runs one of its generated procedures against your target database, it dumps the exact SQL it sent to a companion `.sql` file. If the procedure throws, the progress log surfaces the file path via `Debug Script:` (see [Failure artifacts](#failure-artifacts) above). Open it in your query tool of choice, re-run the SQL by hand, and reproduce or narrow the problem without guessing what SchemaSmith actually executed.

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

Each run overwrites the debug files for the operations it actually performed. Operations that don't apply to your platform (for example, `Indexed Views` on PostgreSQL or `Materialized Views` on MySQL) produce no file. Debug files land in the `ArtifactPath` directory (default: current working directory); `--LogPath` controls the progress and error logs, not artifact or debug SQL.

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

## DropTablesRemovedFromProduct

Controls whether SchemaQuench drops tables that exist in the database but no longer appear in the schema package. Three tiers compose to produce the effective value, resolved environment → product → template.

| Scope | Where to set | Default |
|---|---|---|
| Environment | `DropTablesRemovedFromProduct` in `SchemaQuench.settings.json` (or `SmithySettings_DropTablesRemovedFromProduct` environment variable) | `true` |
| Product | `DropTablesRemovedFromProduct` in `Product.json` | `true` |
| Template | `DropTablesRemovedFromProduct` in `Template.json` | (inherit) |

A `false` at any tier is sticky — it locks the effective value to `false` for all lower tiers and cannot be re-enabled by a more-specific setting. Absent (not set) inherits from the tier above. A `true` at a lower tier overrides an inherited `true` but never an ancestor's `false`.

```json
// SchemaQuench.settings.json — turn off auto-drop for this environment
{ "DropTablesRemovedFromProduct": false }
```

```json
// Product.json — package declares its tables must never be auto-dropped
{ "DropTablesRemovedFromProduct": false }
```

```json
// Template.json — suppress auto-drop for this template only
{ "DropTablesRemovedFromProduct": false }
```

> **Note:** All tiers absent preserves existing behavior — if you haven't set any, table-drop-by-absence works exactly as it always has.

For full guidance — environment advice, the rollback-friendly removal pattern, and the recyclebin alternative — see [DropTablesRemovedFromProduct](schemaquench.md#droptablesremovedfromproduct) in the SchemaQuench reference.

---

## DropColumnsRemovedFromProduct

Controls whether SchemaQuench drops columns that exist in the database but no longer appear in the table JSON. Four tiers compose to produce the effective value, resolved environment → product → template → table.

| Scope | Where to set | Default |
|---|---|---|
| Environment | `DropColumnsRemovedFromProduct` in `SchemaQuench.settings.json` (or `SmithySettings_DropColumnsRemovedFromProduct` environment variable) | `true` |
| Product | `DropColumnsRemovedFromProduct` in `Product.json` | (inherit) |
| Template | `DropColumnsRemovedFromProduct` in `Template.json` | (inherit) |
| Table | `DropColumnsRemovedFromProduct` in a table's `.json` file | (inherit) |

A `false` at any tier is sticky — it locks the effective value to `false` for all lower tiers and cannot be re-enabled by a more-specific setting. Absent inherits from the tier above. A `true` at a lower tier overrides an inherited `true` but never an ancestor's explicit `false`. The table tier can only tighten (set its own `false` to protect its columns); a table `true` cannot re-enable column drops that a higher tier suppressed.

```json
// SchemaQuench.settings.json — suppress column drops for all products in this environment
{ "DropColumnsRemovedFromProduct": false }
```

```json
// Product.json — package declares its columns must never be auto-dropped
{ "DropColumnsRemovedFromProduct": false }
```

```json
// Template.json — suppress column drops for this template only
{ "DropColumnsRemovedFromProduct": false }
```

```json
// Tables/dbo.AuditLog.json — protect this table's columns regardless of higher-tier settings
{ "Name": "AuditLog", "DropColumnsRemovedFromProduct": false }
```

> **Note:** All tiers absent preserves existing behavior — if you haven't set any, column-drop-by-absence works exactly as it always has (default `true`).

For full guidance — environment advice, the four-tier cascade detail, and the rollback-friendly removal pattern — see [DropColumnsRemovedFromProduct](schemaquench.md#dropcolumnsremovedfromproduct) in the SchemaQuench reference.

---

## DropForeignKeysRemovedFromProduct

Controls whether SchemaQuench drops foreign keys that exist in the database but no longer appear in the table JSON. Four tiers compose to produce the effective value, resolved environment → product → template → table.

| Scope | Where to set | Default |
|---|---|---|
| Environment | `DropForeignKeysRemovedFromProduct` in `SchemaQuench.settings.json` (or `SmithySettings_DropForeignKeysRemovedFromProduct` environment variable) | `true` |
| Product | `DropForeignKeysRemovedFromProduct` in `Product.json` | (inherit) |
| Template | `DropForeignKeysRemovedFromProduct` in `Template.json` | (inherit) |
| Table | `DropForeignKeysRemovedFromProduct` in a table's `.json` file | (inherit) |

Same explicit-false-sticky semantics as the other drop-control flags: a `false` at any tier locks the effective value for all lower tiers, and a table can tighten to `false` but never re-enable a higher-tier suppression. Only by-absence removal is gated — a foreign key whose definition merely changed is always dropped and recreated. On MySQL this flag also decouples foreign-key cleanup from `DropUnknownIndexes`, matching SQL Server and PostgreSQL.

> **Note:** All tiers absent preserves existing behavior (default `true`).

For full guidance, see [DropForeignKeysRemovedFromProduct](schemaquench.md#dropforeignkeysremovedfromproduct) in the SchemaQuench reference.

---

## DropCheckConstraintsRemovedFromProduct

Controls whether SchemaQuench drops table-level CHECK constraints that exist in the database but no longer appear in the table JSON. Four tiers compose to produce the effective value, resolved environment → product → template → table.

| Scope | Where to set | Default |
|---|---|---|
| Environment | `DropCheckConstraintsRemovedFromProduct` in `SchemaQuench.settings.json` (or `SmithySettings_DropCheckConstraintsRemovedFromProduct` environment variable) | `true` |
| Product | `DropCheckConstraintsRemovedFromProduct` in `Product.json` | (inherit) |
| Template | `DropCheckConstraintsRemovedFromProduct` in `Template.json` | (inherit) |
| Table | `DropCheckConstraintsRemovedFromProduct` in a table's `.json` file | (inherit) |

Same explicit-false-sticky semantics as the other drop-control flags: a `false` at any tier locks the effective value for all lower tiers, and a table can tighten to `false` but never re-enable a higher-tier suppression. Only by-absence removal is gated (a modified check still reconciles), and column-level `CheckExpression` checks are out of scope. With the flag on, SQL Server and MySQL now drop orphaned table-level checks by absence, matching PostgreSQL.

> **Note:** All tiers absent preserves existing behavior (default `true`).

For full guidance, see [DropCheckConstraintsRemovedFromProduct](schemaquench.md#dropcheckconstraintsremovedfromproduct) in the SchemaQuench reference.

---

## DropUnknownIndexes

Controls whether SchemaQuench drops indexes on managed tables that aren't defined in the schema package. Three tiers compose to produce the effective value, resolved environment → product → template.

| Scope | Where to set | Default |
|---|---|---|
| Environment | `DropUnknownIndexes` in `SchemaQuench.settings.json` (or `SmithySettings_DropUnknownIndexes` environment variable) | `false` |
| Product | `DropUnknownIndexes` in `Product.json` | (inherit) |
| Template | `DropUnknownIndexes` in `Template.json` | (inherit) |

A `false` at any tier is sticky — it locks the effective value to `false` for all lower tiers and cannot be re-enabled by a more-specific setting. Absent inherits from the tier above. A `true` at a lower tier overrides an inherited `true` (or default `false`) but never an ancestor's explicit `false`.

The environment tier is new in this release. Previously `DropUnknownIndexes` was settable only in `Product.json` and `Template.json`. It can now be set or suppressed in `SchemaQuench.settings.json` (or via environment variable) as a deployment-wide guardrail.

```json
// SchemaQuench.settings.json — suppress index drops for all products in this environment
{ "DropUnknownIndexes": false }
```

```json
// SchemaQuench.settings.json — enable index drops for all products in this environment
{ "DropUnknownIndexes": true }
```

> **Note:** Default `false` at all tiers preserves existing behavior — if you haven't set any tier, index-drop-by-absence is off, consistent with prior releases. The environment tier adds an opt-in or opt-out guardrail without changing the per-package default.

For package-side configuration and adoption guidance, see [DropUnknownIndexes](schema-packages.md#properties) in the Schema Packages reference.

---

## Related Documentation

- [SchemaQuench Reference](schemaquench.md) -- Deployment engine settings and behavior
- [SchemaTongs Reference](schematongs.md) -- Schema extraction settings
- [DataTongs Reference](datatongs.md) -- Data extraction settings
- [Script Tokens Reference](script-tokens.md) -- Token replacement via ScriptTokens config
- [CI/CD Integration](../guide/07-cicd-integration.md#configuration-via-environment-variables) -- Practical guide to environment variable configuration in pipelines
