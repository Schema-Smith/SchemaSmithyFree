# DataTongs Configuration

Applies to: DataTongs (SQL Server, Community)

## Configuration Loading Order

DataTongs reads its configuration from multiple sources, merged in the following precedence order (highest priority last):

1. **Configuration file** -- `DataTongs.settings.json` in the current working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

---

## DataTongs.settings.json

```json
{
    "Source": {
        "Server": "",
        "Port": "",
        "User": "",
        "Password": "",
        "Database": "",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    },
    "OutputPath": "",
    "Tables": [],
    "ShouldCast": {
        "DisableTriggers": true,
        "MergeUpdate": true,
        "MergeDelete": false
    }
}
```

---

## Configuration Keys

### Source Connection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Source:Server` | string | _(required)_ | SQL Server instance name |
| `Source:Port` | string | _(empty)_ | SQL Server port number. When provided, appended to the server as `server,port`. The `server,port` format in `Server` is also supported. |
| `Source:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Source:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |
| `Source:Database` | string | _(required)_ | Source database name to extract data from |
| `Source:ConnectionProperties` | object | _(empty)_ | Arbitrary key-value pairs appended to the connection string. See [Connection Properties](#connection-properties) below. |

### Output

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `OutputPath` | string | _(required)_ | Directory where generated MERGE scripts are written. Each table produces a `Populate schema.tablename.sql` file. |

### Tables Array

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Name` | string | _(required)_ | Table name in `schema.table` format. If no schema prefix, `dbo` is assumed. |
| `KeyColumns` | string | _(required)_ | Comma-separated column names for the MERGE `ON` clause. |
| `Filter` | string | _(empty)_ | SQL `WHERE` clause (without `WHERE`) to filter extracted rows. Also applied to `WHEN NOT MATCHED BY SOURCE` when `MergeDelete` is enabled. |

### Script Generation (ShouldCast)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ShouldCast:DisableTriggers` | bool | `true` | When `true`, wraps each MERGE with `DISABLE TRIGGER ALL` and `ENABLE TRIGGER ALL`. |
| `ShouldCast:MergeUpdate` | bool | `true` | When `true`, includes `WHEN MATCHED THEN UPDATE` in MERGE scripts. |
| `ShouldCast:MergeDelete` | bool | `false` | When `true`, includes `WHEN NOT MATCHED BY SOURCE THEN DELETE` in MERGE scripts. |

---

## Environment Variable Overrides

Configuration keys can be overridden using environment variables. For the general pattern, see [Getting Started — Configuration Loading](../getting-started.md#configuration-loading).

> **Note:** The `Tables` array cannot be configured via environment variables. Use the JSON configuration file for table definitions.

### Environment Variable Mapping

| Configuration Key | SmithySettings_ Variable |
|---|---|
| `Source:Server` | `SmithySettings_Source__Server` |
| `Source:Port` | `SmithySettings_Source__Port` |
| `Source:User` | `SmithySettings_Source__User` |
| `Source:Password` | `SmithySettings_Source__Password` |
| `Source:Database` | `SmithySettings_Source__Database` |
| `Source:ConnectionProperties:<name>` | `SmithySettings_Source__ConnectionProperties__<name>` |
| `OutputPath` | `SmithySettings_OutputPath` |
| `ShouldCast:DisableTriggers` | `SmithySettings_ShouldCast__DisableTriggers` |
| `ShouldCast:MergeUpdate` | `SmithySettings_ShouldCast__MergeUpdate` |
| `ShouldCast:MergeDelete` | `SmithySettings_ShouldCast__MergeDelete` |

### Example

```bash
export SmithySettings_Source__Server="myserver\\SQLEXPRESS"
export SmithySettings_Source__Database="ProductionDB"
export SmithySettings_ShouldCast__MergeDelete="true"
export SmithySettings_OutputPath="/output/scripts"
```

---

## Command-Line Options

DataTongs accepts the common CLI switches shared by all tools. See [CLI Options](../cli-options.md) for the full reference.

---

## User Secrets

User secrets are available in DEBUG builds only.

```bash
dotnet user-secrets set "Source:Server" "localhost"
dotnet user-secrets set "Source:Database" "MyDatabase"
```

---

## Connection Configuration

DataTongs connects to SQL Server using the `Source` settings. The authentication mode is determined by the `User` and `Password` fields:

- **Windows authentication** (default) — Used when `User` and `Password` are both blank.
- **SQL Server authentication** — Used when both `User` and `Password` are provided.

### Connection Properties

`Source:ConnectionProperties` accepts arbitrary key-value pairs that are appended to the connection string. This allows you to configure any ADO.NET SQL Server connection string keyword.

```json
"Source": {
    "Server": "myserver",
    "Database": "ReferenceData",
    "ConnectionProperties": {
        "TrustServerCertificate": "True",
        "Encrypt": "True"
    }
}
```

Common properties:

| Property | Example Value | Description |
|---|---|---|
| `TrustServerCertificate` | `"True"` | Trust the server's SSL certificate without validation. Recommended for local and dev environments. |
| `Encrypt` | `"True"` | Enforce an encrypted connection. |
| `ApplicationIntent` | `"ReadOnly"` | Direct the connection to a readable secondary replica in an availability group. |
| `Column Encryption Setting` | `"Enabled"` | Enable Always Encrypted column decryption. |

Individual properties can also be overridden via environment variables:

```bash
export SmithySettings_Source__ConnectionProperties__TrustServerCertificate="True"
```

> **Breaking change from previous versions:** Earlier releases hardcoded `TrustServerCertificate=True` and `ApplicationIntent=ReadWrite` on every connection. These values are now configurable. The sample settings file includes `TrustServerCertificate: True` as a recommended default. If you relied on `ApplicationIntent=ReadWrite`, add it explicitly to `ConnectionProperties`.

### --ConnectionString Override

The `--ConnectionString` switch bypasses all `Source` settings and passes the provided value directly to the SQL Server driver. Use this for scenarios where you need full control over the connection string.

```
DataTongs --ConnectionString:"data source=myserver;Initial Catalog=mydb;User ID=sa;Password=secret;TrustServerCertificate=True;"
```

When `--ConnectionString` is provided, `Source:Server`, `Source:User`, `Source:Password`, `Source:Port`, `Source:Database`, and `Source:ConnectionProperties` are all ignored.

---

## Example Configuration

```json
{
    "Source": {
        "Server": "dbserver\\SQL2019",
        "Database": "ReferenceData"
    },
    "OutputPath": "C:\\SchemaPackage\\Templates\\Main\\TableData",
    "Tables": [
        { "Name": "dbo.Country", "KeyColumns": "CountryCode" },
        { "Name": "dbo.Currency", "KeyColumns": "CurrencyCode" },
        { "Name": "config.FeatureFlags", "KeyColumns": "FlagName", "Filter": "IsActive = 1" }
    ],
    "ShouldCast": {
        "DisableTriggers": true,
        "MergeUpdate": true,
        "MergeDelete": false
    }
}
```

---

## Related Documentation

- [CLI Options](../cli-options.md)
- [Getting Started](../getting-started.md)
- [DataTongs Overview](README.md)
