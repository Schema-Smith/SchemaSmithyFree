# SchemaQuench Configuration

Applies to: SchemaQuench (SQL Server, Community)

## Configuration Loading Order

SchemaQuench reads its configuration from multiple sources, merged in the following precedence order (highest priority last):

1. **Configuration file** -- `SchemaQuench.settings.json` in the current working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

---

## SchemaQuench.settings.json

```json
{
    "Target": {
        "Server": "",
        "Port": "",
        "User": "",
        "Password": "",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    },
    "WhatIfONLY": false,
    "SchemaPackagePath": "",
    "KindleTheForge": true,
    "UpdateTables": true,
    "DropTablesRemovedFromProduct": true,
    "RunScriptsTwice": false,
    "ScriptTokens": {}
}
```

---

## Configuration Keys

### Target Connection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Target:Server` | string | _(required)_ | SQL Server instance name (e.g., `"myserver"`, `"myserver\\SQLEXPRESS"`, `"myserver,1433"`) |
| `Target:Port` | string | _(empty)_ | SQL Server port number. When provided, appended to the server as `server,port`. The `server,port` format in `Server` is also supported. |
| `Target:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Target:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |
| `Target:ConnectionProperties` | object | _(empty)_ | Arbitrary key-value pairs appended to the connection string. See [Connection Properties](#connection-properties) below. |

### Behavior

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SchemaPackagePath` | string | _(required)_ | Path to the schema package directory or ZIP file. |
| `WhatIfONLY` | bool | `false` | When `true`, runs in dry-run mode. Table quench generates SQL without executing it. Script slots are skipped. |
| `KindleTheForge` | bool | `true` | When `true`, installs SchemaSmith helper functions and stored procedures in the target database. |
| `UpdateTables` | bool | `true` | When `true`, applies table structure changes (columns, indexes, constraints) from the schema package. |
| `DropTablesRemovedFromProduct` | bool | `true` | When `true`, drops tables that exist in the database but are not defined in the schema package. |
| `RunScriptsTwice` | bool | `false` | When `true`, runs object scripts twice to resolve cross-dependencies between objects. |
| `ScriptTokens` | object | `{}` | Config-level override for product script tokens. Keys are token names, values are replacement strings. See [Script Tokens](../script-tokens.md). |

### Script Token Overrides

Token values from `Product.json` can be overridden via the `ScriptTokens` section in `SchemaQuench.settings.json` or environment variables. See [Script Tokens](../script-tokens.md) for the full reference.

---

## Environment Variable Overrides

Configuration keys can be overridden using environment variables. For the general pattern, see [Getting Started — Configuration Loading](../getting-started.md#configuration-loading).

### Environment Variable Mapping

| Configuration Key | SmithySettings_ Variable |
|---|---|
| `Target:Server` | `SmithySettings_Target__Server` |
| `Target:Port` | `SmithySettings_Target__Port` |
| `Target:User` | `SmithySettings_Target__User` |
| `Target:Password` | `SmithySettings_Target__Password` |
| `Target:ConnectionProperties:<name>` | `SmithySettings_Target__ConnectionProperties__<name>` |
| `WhatIfONLY` | `SmithySettings_WhatIfONLY` |
| `SchemaPackagePath` | `SmithySettings_SchemaPackagePath` |
| `KindleTheForge` | `SmithySettings_KindleTheForge` |
| `UpdateTables` | `SmithySettings_UpdateTables` |
| `DropTablesRemovedFromProduct` | `SmithySettings_DropTablesRemovedFromProduct` |
| `RunScriptsTwice` | `SmithySettings_RunScriptsTwice` |
| `ScriptTokens:<name>` | `SmithySettings_ScriptTokens__<name>` |

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

### Connection Properties

`Target:ConnectionProperties` accepts arbitrary key-value pairs that are appended to the connection string. This allows you to configure any ADO.NET SQL Server connection string keyword.

```json
"Target": {
    "Server": "myserver",
    "ConnectionProperties": {
        "TrustServerCertificate": "True",
        "Encrypt": "True",
        "ApplicationIntent": "ReadWrite"
    }
}
```

Common properties:

| Property | Example Value | Description |
|---|---|---|
| `TrustServerCertificate` | `"True"` | Trust the server's SSL certificate without validation. Recommended for local and dev environments. |
| `Encrypt` | `"True"` | Enforce an encrypted connection. |
| `ApplicationIntent` | `"ReadWrite"` | Direct the connection to a primary replica in an availability group. |
| `Column Encryption Setting` | `"Enabled"` | Enable Always Encrypted column decryption. |

Individual properties can also be overridden via environment variables:

```bash
export SmithySettings_Target__ConnectionProperties__TrustServerCertificate="True"
```

> **Breaking change from previous versions:** Earlier releases hardcoded `TrustServerCertificate=True` and `ApplicationIntent=ReadWrite` on every connection. These values are now configurable. The sample settings file includes `TrustServerCertificate: True` as a recommended default. If you relied on `ApplicationIntent=ReadWrite`, add it explicitly to `ConnectionProperties`.

### --ConnectionString Override

The `--ConnectionString` switch bypasses all `Target` settings and passes the provided value directly to the SQL Server driver. Use this for scenarios where you need full control over the connection string, such as CI pipelines or environments where individual fields are inconvenient.

```
SchemaQuench --ConnectionString:"data source=myserver;Initial Catalog=mydb;User ID=sa;Password=secret;TrustServerCertificate=True;"
```

When `--ConnectionString` is provided, `Target:Server`, `Target:User`, `Target:Password`, `Target:Port`, and `Target:ConnectionProperties` are all ignored.

---

## Related Documentation

- [SchemaQuench Overview](README.md)
- [CLI Options](../cli-options.md)
- [Script Tokens](../script-tokens.md)
- [Getting Started](../getting-started.md)
