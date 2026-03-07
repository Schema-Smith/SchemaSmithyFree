# SchemaTongs Configuration

Applies to: SchemaTongs (SQL Server, Community)

## Configuration Loading Order

SchemaTongs reads its configuration from multiple sources, merged in the following precedence order (highest priority last):

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
| `Source:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Source:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |
| `Source:Database` | string | _(required)_ | Source database name to extract from |

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
| `ShouldCast:ScriptDynamicDependencyRemovalForFunctions` | bool | `false` | Generate dynamic SQL to remove dependencies before updating functions |
| `ShouldCast:ObjectList` | string | _(empty)_ | Comma or semicolon-separated list of specific objects to extract. When empty, all matching objects are extracted. |

---

## Environment Variable Overrides

Configuration keys can be overridden using environment variables. For the general pattern, see [Getting Started — Configuration Loading](../getting-started.md#configuration-loading).

### Environment Variable Mapping

| Configuration Key | QuenchSettings_ Variable | SmithySettings_ Variable |
|---|---|---|
| `Source:Server` | `QuenchSettings_Source__Server` | `SmithySettings_Source__Server` |
| `Source:User` | `QuenchSettings_Source__User` | `SmithySettings_Source__User` |
| `Source:Password` | `QuenchSettings_Source__Password` | `SmithySettings_Source__Password` |
| `Source:Database` | `QuenchSettings_Source__Database` | `SmithySettings_Source__Database` |
| `Product:Path` | `QuenchSettings_Product__Path` | `SmithySettings_Product__Path` |
| `Product:Name` | `QuenchSettings_Product__Name` | `SmithySettings_Product__Name` |
| `Template:Name` | `QuenchSettings_Template__Name` | `SmithySettings_Template__Name` |
| `ShouldCast:Tables` | `QuenchSettings_ShouldCast__Tables` | `SmithySettings_ShouldCast__Tables` |
| `ShouldCast:ObjectList` | `QuenchSettings_ShouldCast__ObjectList` | `SmithySettings_ShouldCast__ObjectList` |

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

All connections are established with `TrustServerCertificate` enabled.

---

## Product and Template Naming Defaults

If `Product:Name` is blank, SchemaTongs uses the name of the product directory (the last segment of `Product:Path`).

If `Template:Name` is blank, SchemaTongs uses `"Default"` as the template name.

---

## Related Documentation

- [SchemaTongs Overview](README.md)
- [CLI Options](../cli-options.md)
- [Getting Started](../getting-started.md)
