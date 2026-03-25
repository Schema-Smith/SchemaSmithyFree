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
        "Name": "",
        "CheckConstraintStyle": "ColumnLevel"
    },
    "Template": {
        "Name": ""
    },
    "OrphanHandling": {
        "Mode": "Detect"
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
        "ValidateScripts": false,
        "SaveInvalidScripts": true,
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
| `Product:CheckConstraintStyle` | string | `ColumnLevel` | Controls how check constraints are written when initializing a new `Product.json`. `ColumnLevel` (default) keeps check constraints as column-level `CheckExpression` properties. `TableLevel` promotes all check constraints to named table-level entries. Only applied when creating a new product — does not modify an existing `Product.json`. |
| `Template:Name` | string | _(empty)_ | Template name. If blank, defaults to `"Default"`. |

### Orphan Handling

Orphaned scripts are files that exist in the schema package but no longer correspond to any object in the source database. On re-extraction, SchemaTongs can detect and optionally remove them.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `OrphanHandling:Mode` | string | `Detect` | Controls what happens to orphaned scripts. See [Orphan Handling Modes](#orphan-handling-modes) below. |

#### Orphan Handling Modes

| Mode | Behavior |
|------|----------|
| `Detect` | Orphaned files are logged as warnings. No files are modified. |
| `DetectWithCleanupScripts` | Orphaned files are logged. For each orphan in a script folder (not `Tables/`), a cleanup script is generated and placed in `MigrationScripts/After/` that drops the corresponding database object. |
| `DetectDeleteAndCleanup` | Orphaned files are deleted from the package. Cleanup scripts are generated in `MigrationScripts/After/` as with `DetectWithCleanupScripts`. |

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
| `ShouldCast:ValidateScripts` | bool | `false` | When `true`, each extracted SQL script is parsed for syntax errors after extraction. Scripts that fail validation are saved with a `.sqlerror` extension (or removed if `SaveInvalidScripts` is `false`). See [Script Validation](#script-validation) below. |
| `ShouldCast:SaveInvalidScripts` | bool | `true` | When `ValidateScripts` is enabled and `SaveInvalidScripts` is `true` (the default), scripts that fail validation are saved with a `.sqlerror` extension instead of `.sql`. When `false`, invalid scripts are not written to disk at all. |
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
| `Product:CheckConstraintStyle` | `SmithySettings_Product__CheckConstraintStyle` |
| `Template:Name` | `SmithySettings_Template__Name` |
| `OrphanHandling:Mode` | `SmithySettings_OrphanHandling__Mode` |
| `ShouldCast:Tables` | `SmithySettings_ShouldCast__Tables` |
| `ShouldCast:ValidateScripts` | `SmithySettings_ShouldCast__ValidateScripts` |
| `ShouldCast:SaveInvalidScripts` | `SmithySettings_ShouldCast__SaveInvalidScripts` |
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

## Script Validation

When `ShouldCast:ValidateScripts` is `true`, each extracted SQL script is tested for syntax errors immediately after extraction. SchemaTongs parses the script using the same batch splitter used by SchemaQuench.

Scripts that fail validation are treated according to `ShouldCast:SaveInvalidScripts`:

- **`SaveInvalidScripts: true` (default)** — The script is saved with a `.sqlerror` extension instead of `.sql`. The `.sqlerror` file is not executed by SchemaQuench (which only processes `.sql` files), but it is visible in SchemaHammer with an error indicator.
- **`SaveInvalidScripts: false`** — The script is not written to disk at all.

### When Validation Fails

Validation failures are not always genuine errors. Common false positives include:

- **Cross-database references** — Scripts that reference objects in another database (e.g., `OtherDB.dbo.MyTable`) may fail validation if the cross-database context is not available at parse time.
- **Temporary objects** — References to temp tables or variables created earlier in the same batch.

If a `.sqlerror` file is a false positive, you can manually rename it from `.sqlerror` to `.sql`. SchemaQuench will then execute it on the next quench run.

### .sqlerror Files

`.sqlerror` files are SQL scripts that failed extraction validation. They serve as a record of potential problems without blocking extraction of the rest of the package.

| Tool | Behavior |
|------|----------|
| **SchemaQuench** | Skips `.sqlerror` files — only `.sql` files are loaded and executed |
| **SchemaHammer** | Displays `.sqlerror` files in the script tree with an error indicator |
| **SchemaTongs** | On re-extraction with `SaveInvalidScripts: true`, overwrites `.sqlerror` with the latest extracted content (still as `.sqlerror` if still invalid) |

To include a `.sqlerror` script in the next quench: rename it to `.sql`.

---

## Product and Template Naming Defaults

If `Product:Name` is blank, SchemaTongs uses the name of the product directory (the last segment of `Product:Path`).

If `Template:Name` is blank, SchemaTongs uses `"Default"` as the template name.

---

## Related Documentation

- [SchemaTongs Overview](README.md)
- [CLI Options](../cli-options.md)
- [Getting Started](../getting-started.md)
