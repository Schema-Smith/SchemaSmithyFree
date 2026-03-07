# DataTongs Configuration

Applies to: DataTongs (SQL Server, Community)

## Configuration Loading Order

DataTongs reads its configuration from multiple sources, merged in the following precedence order (highest priority last):

1. **Configuration file** -- `appsettings.json` in the current working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `QuenchSettings_` or `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

---

## appsettings.json

```json
{
    "Source": {
        "Server": "",
        "User": "",
        "Password": "",
        "Database": ""
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
| `Source:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Source:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |
| `Source:Database` | string | _(required)_ | Source database name to extract data from |

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

| Configuration Key | QuenchSettings_ Variable | SmithySettings_ Variable |
|---|---|---|
| `Source:Server` | `QuenchSettings_Source__Server` | `SmithySettings_Source__Server` |
| `Source:User` | `QuenchSettings_Source__User` | `SmithySettings_Source__User` |
| `Source:Password` | `QuenchSettings_Source__Password` | `SmithySettings_Source__Password` |
| `Source:Database` | `QuenchSettings_Source__Database` | `SmithySettings_Source__Database` |
| `OutputPath` | `QuenchSettings_OutputPath` | `SmithySettings_OutputPath` |
| `ShouldCast:DisableTriggers` | `QuenchSettings_ShouldCast__DisableTriggers` | `SmithySettings_ShouldCast__DisableTriggers` |
| `ShouldCast:MergeUpdate` | `QuenchSettings_ShouldCast__MergeUpdate` | `SmithySettings_ShouldCast__MergeUpdate` |
| `ShouldCast:MergeDelete` | `QuenchSettings_ShouldCast__MergeDelete` | `SmithySettings_ShouldCast__MergeDelete` |

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

All connections are established with `TrustServerCertificate` enabled.

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
