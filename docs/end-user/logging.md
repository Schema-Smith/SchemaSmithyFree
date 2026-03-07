# Logging

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## Log Framework

All CLI tools use Apache Log4Net. Each tool ships with its own embedded logging configuration, which is loaded automatically at startup.

---

## Log Files

Each tool writes two log files per run. Files are overwritten at the start of each run.

| Tool | Progress log | Error log |
|---|---|---|
| SchemaQuench | `SchemaQuench - Progress.log` | `SchemaQuench - Errors.log` |
| SchemaTongs | `SchemaTongs - Progress.log` | `SchemaTongs - Errors.log` |
| DataTongs | `DataTongs - Progress.log` | `DataTongs - Errors.log` |

**Progress log** receives all informational output — startup banner, active configuration, per-object progress, and completion status. It also writes to the console simultaneously.

**Error log** receives only error-level entries and is not written to the console. SQL errors from server execution are captured here.

---

## Log File Location

The default log directory is the directory containing the tool executable. Override it with the `--LogPath` switch:

```
SchemaQuench --LogPath:C:\Logs
SchemaTongs --LogPath:/var/log/schemasmith
```

---

## Startup Configuration Logging

Immediately after loading configuration, every tool logs its active configuration to the progress log. This includes the tool name, platform, edition, and version.

Configuration keys whose names contain `Password` or `Pwd` (case-insensitive) are replaced with `**********` in the log output. All other values are logged as-is.

---

## Console Output

Every message written to the progress log also appears on the console in real time.

---

## Log Backup on Exit

On completion, each tool backs up its log files before the process exits. On an unhandled exception, the tool logs the exception to both logs, then performs the same backup.

The backup process:

1. Determines the log directory (the `--LogPath` value, or the executable directory if not specified).
2. Creates a numbered subdirectory named `<ToolName>.0001`, incrementing the number if that directory already exists (`<ToolName>.0002`, `<ToolName>.0003`, and so on).
3. Copies all files matching `<ToolName> - *.log` into the backup subdirectory.

The log files in the log directory are not deleted after backup. Each run overwrites the base log files and writes a copy into a new numbered subdirectory. This preserves the history of all runs while keeping the base files current.

---

## Exit Codes

| Condition | Exit code |
|---|---|
| Normal completion | `0` |
| One or more database quenches failed | `2` |
| Unhandled exception | `3` |
| Log backup failure | `4` |

---

## Related Documentation

- [CLI Options](cli-options.md)
