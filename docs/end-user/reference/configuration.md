# Configuration Reference

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

This reference covers the configuration system shared by all three SchemaSmith CLI tools: how to pass options on the command line, how settings files and environment variables work, and how they combine. Tool-specific settings are documented in each tool's own reference page.

---

## CLI Switch Format

All switches accept either a double-dash (`--`) or forward-slash (`/`) prefix. Separate the switch name from its value with `:` or `=`. A single leading dash (`-`) also works.

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

Every SchemaSmith CLI tool recognizes these switches. They are processed before any configuration is loaded.

| Switch | Aliases | Description |
|---|---|---|
| `--version` | `-v`, `--ver` | Print the tool name, edition, and version number, then exit. |
| `--help` | `-h`, `-?` | Print the available command-line switches, then exit. |
| `--ConfigFile:<path>` | | Path to the settings file. Overrides the default `<ToolName>.settings.json`. |
| `--LogPath:<path>` | | Directory for log files and backup subdirectories. Defaults to the tool's executable directory. |
| `--ConnectionString:<connstr>` | | Full ADO.NET connection string. When provided, this bypasses all individual connection settings (`Server`, `Port`, `User`, `Password`, `ConnectionProperties`). |

### Examples

```bash
# Deploy a schema package with a custom config and log directory
SchemaQuench --ConfigFile:production.json --LogPath:C:\Logs

# Extract a schema using a specific config
SchemaTongs --ConfigFile:extract-config.json

# Export data merge scripts, overriding the connection entirely
DataTongs --ConnectionString:"Data Source=myserver;Initial Catalog=mydb;User ID=sa;Password=secret;TrustServerCertificate=True;"

# Check which version is installed
SchemaQuench --version

# See available switches
SchemaTongs --help
```

---

## Configuration Hierarchy

SchemaSmith builds configuration by layering sources. Later sources override earlier ones. The full chain, from lowest to highest priority:

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
SchemaQuench --ConnectionString:"Data Source=prod-server;Initial Catalog=mydb;User ID=deploy;Password=s3cret;TrustServerCertificate=True;"
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
2. The **tool's executable directory** (where the `.exe` lives)

If the file is found in the current directory, that copy is used. If not, the tool falls back to the executable directory. If neither location has the file, the tool starts with an empty configuration (any required values must come from environment variables or CLI switches).

To use a different file entirely, pass the `--ConfigFile` switch:

```bash
SchemaQuench --ConfigFile:C:\configs\production.json
```

The path can be absolute or relative to the current working directory.

---

## Environment Variables

All three tools read environment variables prefixed with `SmithySettings_`. The prefix is stripped, and double underscores (`__`) map to hierarchy separators in the configuration structure.

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

Environment variables are especially useful in CI/CD pipelines and containers where you do not want secrets in files on disk.

---

## Connection Configuration

All three tools connect to SQL Server. SchemaQuench uses a `Target` section (it writes to the server). SchemaTongs and DataTongs use a `Source` section (they read from the server). The structure is the same either way.

### Individual connection settings

```json
{
    "Target": {
        "Server": "myserver",
        "Port": "1433",
        "User": "sa",
        "Password": "s3cret",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    }
}
```

| Key | Purpose |
|---|---|
| `Server` | SQL Server hostname or IP address. |
| `Port` | TCP port. Leave blank for the default (1433). |
| `User` | SQL login username. Leave blank for Windows authentication. |
| `Password` | SQL login password. Leave blank for Windows authentication. |
| `Database` | Target database name. Used by SchemaTongs and DataTongs. SchemaQuench reads its target databases from the schema package instead. |
| `ConnectionProperties` | Dictionary of additional connection string properties. Each key-value pair is appended to the built connection string. |

**Windows authentication:** Leave both `User` and `Password` blank. The tool will connect using the identity of the process.

### Full connection string override

Instead of individual settings, you can provide a complete ADO.NET connection string:

```bash
SchemaQuench --ConnectionString:"Data Source=myserver;Initial Catalog=mydb;User ID=sa;Password=s3cret;TrustServerCertificate=True;"
```

When `--ConnectionString` is provided, all individual connection settings (`Server`, `Port`, `User`, `Password`, `ConnectionProperties`) are bypassed.

---

## Password Masking

When a tool logs its active configuration at startup, it masks any value whose key contains `Password` or `Pwd` (case-insensitive). The masked value appears as `**********` in both the progress log and console output.

All other configuration values are logged as-is. This lets you audit the active configuration from the log without exposing credentials.

Example log output:

```
Configuration:
    Server: myserver
    Port: 1433
    User: sa
    Password: **********
    ConnectionProperties:
      TrustServerCertificate: True
```

---

## Logging

### Framework

All CLI tools use [Apache Log4Net](https://logging.apache.org/log4net/). Each tool ships with an embedded Log4Net configuration that is loaded automatically at startup -- there is nothing to configure.

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

### Log file location

By default, logs are written to the tool's executable directory. Override this with `--LogPath`:

```bash
SchemaQuench --LogPath:C:\Logs
SchemaTongs --LogPath:/var/log/schemasmith
DataTongs --LogPath:D:\BuildLogs
```

### Startup configuration dump

Immediately after loading configuration, every tool logs its complete active configuration to the progress log. This includes the tool name, platform (SqlServer), edition (Community), and version number, followed by every configuration key and its value (with passwords masked). This makes it straightforward to verify what settings were in effect for any given run.

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

---

## Exit Codes

| Code | Condition | Recommended action |
|---|---|---|
| `0` | Normal completion | None -- the operation succeeded. |
| `2` | One or more database quenches failed (SchemaQuench only) | Check the progress and error logs for details on which databases failed and why. Fix the failing scripts and re-run. |
| `3` | Unhandled exception | An unexpected error occurred. The exception is logged to both the progress and error logs before exit. Report the error with the log contents if the cause is not obvious. |
| `4` | Log backup failure | The tool completed its main work but could not back up the log files. Check directory permissions and disk space in the log directory. The base log files may still be readable even though the backup failed. |

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

# Override connection via CLI
SchemaQuench --ConnectionString:"Data Source=prod-db;Initial Catalog=mydb;User ID=deploy;Password=s3cret;TrustServerCertificate=True;"
```
