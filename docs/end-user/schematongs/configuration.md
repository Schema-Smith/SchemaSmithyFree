# SchemaTongs Configuration

Applies to: SchemaTongs (SQL Server, Community)

## Configuration Loading Order

SchemaTongs reads its configuration from multiple sources, merged in the following precedence order (highest priority last):

1. **Configuration file** -- `SchemaTongs.settings.json` in the current working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

---

## SchemaTongs.settings.json

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
    "Product": {
        "Path": "",
        "Name": ""
    },
    "Template": {
        "Name": ""
    },
    "ShouldCast": {
        "Tables": true,
        "Schemas": true,
        "UserDefinedTypes": true,
        "UserDefinedFunctions": true,
        "Views": true,
        "StoredProcedures": true,
        "TableTriggers": true,
        "Catalogs": true,
        "StopLists": true,
        "DDLTriggers": true,
        "XMLSchemaCollections": true,
        "IndexedViews": true,
        "ScriptDynamicDependencyRemovalForFunctions": false,
        "ObjectList": ""
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
| `Source:Database` | string | _(required)_ | Source database name to extract from |
| `Source:ConnectionProperties` | object | _(empty)_ | Arbitrary key-value pairs appended to the connection string. See [Connection Properties](#connection-properties) below. |

### Output

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Product:Path` | string | _(required)_ | Directory where the schema package will be created or updated |
| `Product:Name` | string | _(empty)_ | Product name written to `Product.json`. If blank, defaults to the directory name. |
| `Template:Name` | string | _(empty)_ | Template name. If blank, defaults to `"Default"`. |

### Extraction Control (ShouldCast)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ShouldCast:Tables` | bool | `true` | Extract table definitions as JSON |
| `ShouldCast:Schemas` | bool | `true` | Extract schema creation scripts |
| `ShouldCast:UserDefinedTypes` | bool | `true` | Extract user-defined data types and table types |
| `ShouldCast:UserDefinedFunctions` | bool | `true` | Extract scalar and table-valued functions |
| `ShouldCast:Views` | bool | `true` | Extract view definitions |
| `ShouldCast:StoredProcedures` | bool | `true` | Extract stored procedures |
| `ShouldCast:TableTriggers` | bool | `true` | Extract DML triggers |
| `ShouldCast:Catalogs` | bool | `true` | Extract full-text catalogs |
| `ShouldCast:StopLists` | bool | `true` | Extract full-text stop lists |
| `ShouldCast:DDLTriggers` | bool | `true` | Extract database-level DDL triggers |
| `ShouldCast:XMLSchemaCollections` | bool | `true` | Extract XML schema collections |
| `ShouldCast:IndexedViews` | bool | `true` | Extract indexed (materialized) view definitions |
| `ShouldCast:ScriptDynamicDependencyRemovalForFunctions` | bool | `false` | Generate dynamic SQL to remove dependencies before updating functions |
| `ShouldCast:ObjectList` | string | _(empty)_ | Comma or semicolon-separated list of specific objects to extract. When empty, all matching objects are extracted. |

---

## Environment Variable Overrides

Configuration keys can be overridden using environment variables. For the general pattern, see [Getting Started — Configuration Loading](../getting-started.md#configuration-loading).

### Environment Variable Mapping

| Configuration Key | SmithySettings_ Variable |
|---|---|
| `Source:Server` | `SmithySettings_Source__Server` |
| `Source:Port` | `SmithySettings_Source__Port` |
| `Source:User` | `SmithySettings_Source__User` |
| `Source:Password` | `SmithySettings_Source__Password` |
| `Source:Database` | `SmithySettings_Source__Database` |
| `Source:ConnectionProperties:<name>` | `SmithySettings_Source__ConnectionProperties__<name>` |
| `Product:Path` | `SmithySettings_Product__Path` |
| `Product:Name` | `SmithySettings_Product__Name` |
| `Template:Name` | `SmithySettings_Template__Name` |
| `ShouldCast:Tables` | `SmithySettings_ShouldCast__Tables` |
| `ShouldCast:ObjectList` | `SmithySettings_ShouldCast__ObjectList` |

### Example

```bash
export SmithySettings_Source__Server="myserver\\SQLEXPRESS"
export SmithySettings_Source__Database="ProductionDB"
export SmithySettings_Product__Path="/output/MyProduct"
export SmithySettings_ShouldCast__StoredProcedures="false"
```

---

## Command-Line Options

SchemaTongs accepts the common CLI switches shared by all tools. See [CLI Options](../cli-options.md) for the full reference.

---

## User Secrets

User secrets are available in DEBUG builds only.

```bash
dotnet user-secrets set "Source:Server" "localhost"
dotnet user-secrets set "Source:Database" "MyDatabase"
```

---

## Connection Configuration

SchemaTongs connects to SQL Server using the `Source` settings. The authentication mode is determined by the `User` and `Password` fields:

- **Windows authentication** (default) — Used when `User` and `Password` are both blank.
- **SQL Server authentication** — Used when both `User` and `Password` are provided.

### Connection Properties

`Source:ConnectionProperties` accepts arbitrary key-value pairs that are appended to the connection string. This allows you to configure any ADO.NET SQL Server connection string keyword.

```json
"Source": {
    "Server": "myserver",
    "Database": "ProductionDB",
    "ConnectionProperties": {
        "TrustServerCertificate": "True",
        "Encrypt": "True",
        "ApplicationIntent": "ReadOnly"
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
SchemaTongs --ConnectionString:"data source=myserver;Initial Catalog=mydb;User ID=sa;Password=secret;TrustServerCertificate=True;"
```

When `--ConnectionString` is provided, `Source:Server`, `Source:User`, `Source:Password`, `Source:Port`, `Source:Database`, and `Source:ConnectionProperties` are all ignored.

---

## Product and Template Naming Defaults

If `Product:Name` is blank, SchemaTongs uses the name of the product directory (the last segment of `Product:Path`).

If `Template:Name` is blank, SchemaTongs uses `"Default"` as the template name.

---

## Related Documentation

- [SchemaTongs Overview](README.md)
- [CLI Options](../cli-options.md)
- [Getting Started](../getting-started.md)
