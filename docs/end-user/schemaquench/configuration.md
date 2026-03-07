# SchemaQuench Configuration

Applies to: SchemaQuench (SQL Server, Community)

## Configuration Loading Order

SchemaQuench reads its configuration from multiple sources, merged in the following precedence order (highest priority last):

1. **Configuration file** -- `appsettings.json` in the current working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `QuenchSettings_` or `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

---

## appsettings.json

```json
{
    "Target": {
        "Server": "",
        "User": "",
        "Password": ""
    },
    "WhatIfONLY": false,
    "SchemaPackagePath": ""
}
```

---

## Configuration Keys

### Target Connection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Target:Server` | string | _(required)_ | SQL Server instance name (e.g., `"myserver"`, `"myserver\\SQLEXPRESS"`, `"myserver,1433"`) |
| `Target:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Target:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |

### Behavior

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SchemaPackagePath` | string | _(required)_ | Path to the schema package directory or ZIP file. |
| `WhatIfONLY` | bool | `false` | When `true`, runs in dry-run mode. Table quench generates SQL without executing it. Script slots are skipped. |

### Script Token Overrides

Token values from `Product.json` can be overridden via the `ScriptTokens` section in `appsettings.json` or environment variables. See [Script Tokens](../script-tokens.md) for the full reference.

---

## Environment Variable Overrides

Configuration keys can be overridden using environment variables. For the general pattern, see [Getting Started — Configuration Loading](../getting-started.md#configuration-loading).

### Environment Variable Mapping

| Configuration Key | QuenchSettings_ Variable | SmithySettings_ Variable |
|---|---|---|
| `Target:Server` | `QuenchSettings_Target__Server` | `SmithySettings_Target__Server` |
| `Target:User` | `QuenchSettings_Target__User` | `SmithySettings_Target__User` |
| `Target:Password` | `QuenchSettings_Target__Password` | `SmithySettings_Target__Password` |
| `WhatIfONLY` | `QuenchSettings_WhatIfONLY` | `SmithySettings_WhatIfONLY` |
| `SchemaPackagePath` | `QuenchSettings_SchemaPackagePath` | `SmithySettings_SchemaPackagePath` |
| `ScriptTokens:<name>` | `QuenchSettings_ScriptTokens__<name>` | `SmithySettings_ScriptTokens__<name>` |

### Example

```bash
export SmithySettings_Target__Server="sql-prod-01"
export SmithySettings_Target__User="deploy_user"
export SmithySettings_Target__Password="s3cur3Pa$$"
export SmithySettings_SchemaPackagePath="/packages/MyProduct"
export SmithySettings_ScriptTokens__ReleaseVersion="2.1.0"
```

---

## Command-Line Options

SchemaQuench accepts the common CLI switches shared by all tools. See [CLI Options](../cli-options.md) for the full reference.

---

## User Secrets

User secrets are available in DEBUG builds only and are not present in release distributions.

```bash
dotnet user-secrets set "Target:Server" "localhost"
dotnet user-secrets set "Target:Password" "devPassword123"
```

User secrets are loaded after the JSON configuration file and before environment variables.

---

## Connection Configuration

SchemaQuench connects to SQL Server using the `Target` settings. The authentication mode is determined by the `User` and `Password` fields:

- **Windows authentication** (default) — Used when `User` and `Password` are both blank. The connection uses the identity of the account running SchemaQuench.
- **SQL Server authentication** — Used when both `User` and `Password` are provided.

All connections are established with `TrustServerCertificate` enabled.

---

## Related Documentation

- [SchemaQuench Overview](README.md)
- [CLI Options](../cli-options.md)
- [Script Tokens](../script-tokens.md)
- [Getting Started](../getting-started.md)
