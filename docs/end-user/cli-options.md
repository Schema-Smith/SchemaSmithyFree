# CLI Options

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## Switch Format

All switches accept either a double-dash (`--`) or forward-slash (`/`) prefix. Values are separated from the switch name by either `:` or `=`. Flags that take no value are specified without a separator.

```
--switch:value
/switch:value
--switch=value
--flag
/flag
```

Switch names are case-insensitive. Values containing spaces must be quoted.

```
--ConfigFile:"C:\configs\my config.json"
```

---

## Common Switches

These switches are recognized by all three CLI tools.

| Switch | Aliases | Description |
|---|---|---|
| `--version` | `-v`, `--ver` | Print the tool version, then exit. |
| `--help` | `-h`, `-?` | Print command-line options, then exit. |
| `--ConfigFile:<path>` | | Path to the configuration file. Overrides the default `appsettings.json` in the current working directory. |
| `--LogPath:<path>` | | Directory for log output and backup directories. Defaults to the executable directory. |

---

## Examples

```
SchemaQuench --ConfigFile:production.json --LogPath:C:\Logs

SchemaTongs --ConfigFile:extract-config.json

SchemaQuench --version

SchemaTongs --help
```
