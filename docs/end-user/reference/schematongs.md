# SchemaTongs Reference

Cast your live database into version-controlled code with a single command. SchemaTongs grips every object in your SQL Server database -- all 13 types -- and extracts them into a clean schema package: tables as JSON, programmable objects as SQL scripts, everything organized and ready to commit to source control. Point it at a database, run it, and you have a deployable package that SchemaQuench and SchemaHammer can work with immediately.

---

## Installation

**GitHub Releases** -- Download the latest release from the [SchemaSmith releases page](https://github.com/Schema-Smith/SchemaSmithyFree/releases). Unzip and place the executable on your PATH.

**Chocolatey** -- Install with a single command:

```
choco install schematongs-dotnetcore10
```

---

## Running SchemaTongs

From the directory containing your `SchemaTongs.settings.json`:

```bash
SchemaTongs
```

With a custom config file:

```bash
SchemaTongs --ConfigFile:C:\configs\extract-staging.json
```

With a full connection string override:

```bash
SchemaTongs --ConnectionString:"Data Source=myserver;Initial Catalog=NorthwindDev;User ID=sa;Password=secret;TrustServerCertificate=True;"
```

SchemaTongs connects to the source database, reads every enabled object type, and writes the results to the configured product path. On first run it creates the full package structure; on subsequent runs it updates existing files in place.

---

## Configuration Reference

SchemaTongs reads configuration from multiple sources, merged in precedence order (highest priority last):

1. **Settings file** -- `SchemaTongs.settings.json` in the working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

For the full explanation of configuration loading, settings file format, environment variable mapping, and CLI switches, see the [Configuration Reference](configuration.md).

### Complete SchemaTongs.settings.json

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

### Source Connection

| Key | Type | Default | Description |
|---|---|---|---|
| `Source:Server` | string | _(required)_ | SQL Server instance name or hostname. |
| `Source:Port` | string | _(empty)_ | Port number. When provided, appended as `server,port`. You can also embed the port directly in `Server` using the comma syntax. |
| `Source:User` | string | _(empty)_ | SQL Server login. Leave blank for Windows authentication. |
| `Source:Password` | string | _(empty)_ | SQL Server password. Leave blank for Windows authentication. |
| `Source:Database` | string | _(required)_ | Name of the database to extract from. |
| `Source:ConnectionProperties` | object | `{}` | Arbitrary key-value pairs appended to the connection string. Allows any ADO.NET connection string keyword. |

Common connection properties:

| Property | Example | Purpose |
|---|---|---|
| `TrustServerCertificate` | `"True"` | Trust the server certificate without validation. Recommended for local and dev environments. |
| `Encrypt` | `"True"` | Enforce an encrypted connection. |
| `ApplicationIntent` | `"ReadOnly"` | Route the connection to a readable secondary in an availability group. |
| `Column Encryption Setting` | `"Enabled"` | Enable Always Encrypted column decryption. |

The `--ConnectionString` switch bypasses all `Source` settings entirely. When provided, `Server`, `Port`, `User`, `Password`, `Database`, and `ConnectionProperties` are all ignored.

---

### Product Output

| Key | Type | Default | Description |
|---|---|---|---|
| `Product:Path` | string | _(required)_ | Directory where the schema package is created or updated. |
| `Product:Name` | string | _(directory name)_ | Product name written to `Product.json`. If blank, defaults to the last segment of `Product:Path`. |
| `Product:CheckConstraintStyle` | string | `ColumnLevel` | Controls how check constraints are written when creating a new `Product.json`. See [CheckConstraintStyle](#checkconstraintstyle). |
| `Template:Name` | string | Source database name | Template name. Creates the template directory under `Templates/<Name>/`. Defaults to the `Source:Database` value when not specified. |

---

### ShouldCast Flags

Control exactly what gets cast from the database. Each object type can be individually enabled or disabled. All extraction flags default to `true` except `ScriptDynamicDependencyRemovalForFunctions`, `ValidateScripts`, and `ObjectList`.

| Flag | Type | Default | What It Extracts |
|---|---|---|---|
| `Tables` | bool | `true` | Table definitions as JSON files |
| `Schemas` | bool | `true` | Schema creation scripts |
| `UserDefinedTypes` | bool | `true` | User-defined data types and table types |
| `UserDefinedFunctions` | bool | `true` | Scalar, inline table-valued, and multi-statement table-valued functions |
| `Views` | bool | `true` | View definitions |
| `StoredProcedures` | bool | `true` | Stored procedures |
| `TableTriggers` | bool | `true` | DML triggers (INSERT/UPDATE/DELETE triggers on tables) |
| `Catalogs` | bool | `true` | Full-text catalogs |
| `StopLists` | bool | `true` | Full-text stop lists with their stop words |
| `DDLTriggers` | bool | `true` | Database-level DDL triggers |
| `XMLSchemaCollections` | bool | `true` | XML schema collections |
| `IndexedViews` | bool | `true` | Indexed (materialized) view definitions as JSON files |
| `ScriptDynamicDependencyRemovalForFunctions` | bool | `false` | Generates dynamic SQL dependency removal preamble in function scripts. See [ScriptDynamicDependencyRemovalForFunctions](#scriptdynamicdependencyremovalforfunctions). |
| `ValidateScripts` | bool | `false` | Parses each extracted SQL script for syntax errors. See [Script Validation](#script-validation). |
| `SaveInvalidScripts` | bool | `true` | When `ValidateScripts` is enabled, saves failing scripts as `.sqlerror` files instead of discarding them. |
| `ObjectList` | string | _(empty)_ | Comma or semicolon-separated list of specific objects to extract. See [ObjectList Filtering](#objectlist-filtering). |

---

### OrphanHandling

| Key | Type | Default | Description |
|---|---|---|---|
| `OrphanHandling:Mode` | string | `Detect` | Controls behavior for orphaned files. See [Orphan Detection](#orphan-detection). |

---

## Extractable Object Types

SchemaTongs extracts 13 object types. Each type is controlled by its ShouldCast flag and writes to a specific folder within the template directory.

| Object Type | ShouldCast Flag | Folder | File Naming | Format |
|---|---|---|---|---|
| Tables | `Tables` | `Tables/` | `schema.tablename.json` | JSON |
| Indexed Views | `IndexedViews` | `Indexed Views/` | `schema.viewname.json` | JSON |
| Schemas | `Schemas` | `Schemas/` | `schemaname.sql` | SQL |
| Data Types (alias) | `UserDefinedTypes` | `DataTypes/` | `schema.typename.sql` | SQL |
| Table Types | `UserDefinedTypes` | `DataTypes/` | `schema.typename.sql` | SQL |
| Functions | `UserDefinedFunctions` | `Functions/` | `schema.functionname.sql` | SQL |
| Views | `Views` | `Views/` | `schema.viewname.sql` | SQL |
| Stored Procedures | `StoredProcedures` | `Procedures/` | `schema.procedurename.sql` | SQL |
| DML Triggers | `TableTriggers` | `Triggers/` | `schema.tablename.triggername.sql` | SQL |
| Full-Text Catalogs | `Catalogs` | `FullTextCatalogs/` | `catalogname.sql` | SQL |
| Full-Text Stop Lists | `StopLists` | `FullTextStopLists/` | `stoplistname.sql` | SQL |
| DDL Triggers | `DDLTriggers` | `DDLTriggers/` | `triggername.sql` | SQL |
| XML Schema Collections | `XMLSchemaCollections` | `XMLSchemaCollections/` | `schema.collectionname.sql` | SQL |

### JSON Object Types

**Tables** are extracted as JSON files using the `SchemaSmith.GenerateTableJSON` stored procedure, which SchemaTongs deploys to the source database before extraction. The JSON captures the full table structure -- columns, data types, indexes, keys, constraints, and extended properties -- in the same format that SchemaQuench consumes during deployment.

**Indexed Views** are extracted as JSON files using the `SchemaSmith.GenerateIndexedViewJson` function. The JSON captures the view definition along with its clustered and nonclustered indexes.

### SQL Object Types

All other object types are extracted as SQL scripts. SchemaTongs reads the object definition from `sys.sql_modules` and generates a deployment-ready script with:

- **SET ANSI_NULLS** and **SET QUOTED_IDENTIFIER** -- Preserved from the original object's settings.
- **CREATE OR ALTER** -- SchemaTongs rewrites `CREATE` statements to `CREATE OR ALTER` for stored procedures, functions, views, and triggers.
- **Schema-qualified names** -- Object references are bracket-quoted as `[schema].[name]`.
- **IF NOT EXISTS guards** -- Used for schemas, data types, table types, full-text catalogs, full-text stop lists, and XML schema collections where `CREATE OR ALTER` isn't available.

---

## ObjectList Filtering

The `ObjectList` setting restricts extraction to a specific set of objects. Provide a comma or semicolon-separated list of object names:

```json
"ShouldCast": {
    "ObjectList": "dbo.Customer,dbo.vw_ActiveCustomers,usp_GetOrders"
}
```

Names can be specified with or without a schema prefix. Matching is case-insensitive. When `ObjectList` is empty (the default), all objects matching the enabled ShouldCast flags are extracted.

When `ObjectList` is active, orphan detection is automatically disabled -- SchemaTongs can't determine which files are genuinely orphaned when only a subset of objects is being extracted.

---

## Orphan Detection

Databases change, and when objects get dropped or renamed, the old script files linger in your package. SchemaTongs catches these orphans automatically by comparing the set of files written during extraction against the set of files that existed before extraction.

Configure orphan behavior with `OrphanHandling:Mode`:

| Mode | Behavior |
|---|---|
| `Detect` | Orphaned files are logged as warnings. No files are modified or deleted. This is the default. |
| `DetectWithCleanupScripts` | Orphaned files are logged. For each orphan in a script folder, a cleanup script containing a `DROP` statement is generated and written to the log directory as `_OrphanCleanup_<FolderName>.sql`. The orphaned files themselves are left in place. |
| `DetectDeleteAndCleanup` | Orphaned files are deleted from the package. Cleanup `DROP` scripts are generated in the log directory, the same as `DetectWithCleanupScripts`. |

### When to Use Each Mode

- **`Detect`** -- Safe default. Use during routine re-extraction when you want to review orphans manually before taking action.
- **`DetectWithCleanupScripts`** -- Use when you want SchemaTongs to prepare the cleanup work but you want to review the generated scripts and delete orphaned files yourself.
- **`DetectDeleteAndCleanup`** -- Use when you trust the extraction to be authoritative and want a fully clean package after each run. Particularly useful in automated pipelines.

### How It Works

SchemaTongs builds a file index for each extraction folder before extraction begins. As objects are extracted and written to disk, each file is marked as "written." After extraction completes, any indexed file that wasn't written is an orphan. Cleanup scripts from previous runs are archived into numbered `SchemaTongs.NNNN` backup directories before new ones are generated.

Orphan detection only runs for object types that were fully extracted (ShouldCast flag enabled and no `ObjectList` filter active).

### Choosing a Mode

The core tension: an "orphan" might be a script for an object that was genuinely removed from the database, OR it might be a new object you added to the package that has not been deployed yet. Deleting it automatically would destroy your pending work. The default (`Detect`) is conservative because it never destroys anything -- it just tells you.

- **Most teams, most of the time** -- `Detect`. Review the notifications, manually handle cleanup.
- **Planned cleanup sessions** -- `DetectWithCleanupScripts` when you know you have removed objects and want the DROP scripts generated for review.
- **Automated pipelines with high confidence** -- `DetectDeleteAndCleanup` when the extraction source is authoritative and you want the package to exactly mirror the database. Be aware this removes files for objects you have added to the package but haven't deployed yet.

---

## Script Validation

Catch problems at extraction time instead of discovering them during deployment. When `ShouldCast:ValidateScripts` is `true`, SchemaTongs tests each extracted SQL script for syntax errors immediately after writing it. The validation strategy depends on the object type:

- **Views, functions, and stored procedures** -- SchemaTongs creates the object under a temporary GUID-based name inside a transaction, then rolls back. If creation fails, the script is invalid.
- **Triggers** -- SchemaTongs wraps the script with `SET PARSEONLY ON` / `SET PARSEONLY OFF` and executes it, which validates syntax without actually creating the trigger.

### Invalid Script Handling

Scripts that fail validation are handled according to `ShouldCast:SaveInvalidScripts`:

| SaveInvalidScripts | Behavior |
|---|---|
| `true` (default) | The script is saved with a `.sqlerror` extension instead of `.sql`. The original `.sql` file (if any) is removed. |
| `false` | The script isn't written to disk at all. |

### .sqlerror Files

`.sqlerror` files are SQL scripts that failed extraction validation. They serve as a record of potential problems without blocking the rest of the package.

| Tool | Behavior |
|---|---|
| **SchemaQuench** | Skips `.sqlerror` files -- only `.sql` files are loaded and executed. |
| **SchemaHammer** | Displays `.sqlerror` files in the script tree with an error indicator. |
| **SchemaTongs** | On re-extraction, overwrites `.sqlerror` files with the latest content. If the script still fails validation, it stays as `.sqlerror`. If it now passes, it's written as `.sql` and the `.sqlerror` is removed. |

### False Positives

Validation failures aren't always genuine errors. Common false positives include:

- **Cross-database references** -- Scripts referencing objects in another database (e.g., `OtherDB.dbo.MyTable`) may fail when the parser can't resolve the cross-database context.
- **Temporary objects** -- References to temp tables or variables created elsewhere in the same batch.

To override a false positive, rename the file from `.sqlerror` to `.sql`. SchemaQuench will then include it in the next deployment.

### Invalid Object Cleanup Script

When any scripts fail validation, SchemaTongs generates an `_InvalidObjectCleanup.sql` file in the log directory. This script contains diagnostic information for all invalid objects detected during the extraction run.

### Re-Extract Behavior

When you re-extract without validation enabled, objects are written as normal `.sql` files, and any existing `.sqlerror` for the same object is cleaned up. This means a re-extract without validation "promotes" previously-bad scripts to `.sql` -- because without validation, SchemaTongs doesn't know they're bad. If you want to keep identifying bad scripts, keep `ValidateScripts` enabled. If you have fixed the underlying problems and want clean `.sql` files, re-extract normally.

---

## Subfolder Preservation

Organize your scripts however makes sense for your team -- SchemaTongs will respect that structure across re-extractions. Script folders (`Procedures/`, `Functions/`, `Views/`, `Triggers/`, and all others) support user-created subfolders for organizing scripts. For example, you might organize stored procedures by domain:

```
Procedures/
  Orders/
    dbo.usp_GetOrder.sql
    dbo.usp_CreateOrder.sql
  Customers/
    dbo.usp_GetCustomer.sql
```

SchemaTongs preserves these subfolders across re-extraction. Before extraction begins, it builds a file index (`ExtractionFileIndex`) that maps every `.sql`, `.sqlerror`, and `.json` file to its full path, including any subfolder. When writing an extracted object, SchemaTongs checks the index:

- If the file already exists in a subfolder, it's written back to that same subfolder.
- If the file exists in multiple subfolders, a warning is logged and the file is written to the folder root.
- If the file is new (not in the index), it's written to the folder root.

New objects always appear in the root of their folder. Move them into subfolders as desired -- SchemaTongs will remember the location on the next run.

---

## Package Initialization

The first extraction is where your schema package is born. When SchemaTongs runs against a path that doesn't yet contain a schema package, it creates the full structure:

1. Creates the product directory at `Product:Path`.
2. Generates `Product.json` with the configured product name and a `Platform` of `"SqlServer"`.
3. Creates the template directory under `Templates/<TemplateName>/`.
4. Generates `Template.json` with a `DatabaseIdentificationScript` targeting the source database.
5. Creates all standard script folders: `Tables/`, `Schemas/`, `DataTypes/`, `Functions/`, `Views/`, `Procedures/`, `Triggers/`, `FullTextCatalogs/`, `FullTextStopLists/`, `DDLTriggers/`, `XMLSchemaCollections/`, `Indexed Views/`, `Table Data/`, and `MigrationScripts/` (with `Before/`, `After/`, `AfterTablesScripts/`, and `BetweenTablesAndKeys/` subdirectories).
6. Creates a `.json-schemas/` directory with JSON schema validation files for Product, Template, Table, and Indexed View JSON formats.

On subsequent runs against an existing package, SchemaTongs overwrites object scripts and table definitions with the current database state. It doesn't modify `Product.json` or `Template.json`.

### Helper Stored Procedures

On every run, SchemaTongs deploys (or updates) a stored procedure and a scalar function in the source database under the `SchemaSmith` schema:

- `SchemaSmith.GenerateTableJSON` -- Generates the JSON representation of a table's full structure.
- `SchemaSmith.GenerateIndexedViewJson` -- Generates the JSON representation of an indexed view.

These procedures are lightweight, read-only metadata queries. They're installed in the `SchemaSmith` schema, which is excluded from extraction output.

---

## CheckConstraintStyle

The `Product:CheckConstraintStyle` setting controls how check constraints are represented in extracted table JSON. It only takes effect when creating a new `Product.json` -- on existing products, the style is read from the product file itself.

### ColumnLevel (default)

Each check constraint is stored as a `CheckExpression` property on the column it applies to:

```json
{
    "Name": "[Status]",
    "DataType": "varchar(20)",
    "CheckExpression": "[Status] IN ('Active','Inactive','Pending')"
}
```

### TableLevel

All check constraints are promoted to named entries in the table's `CheckConstraints` array:

```json
{
    "CheckConstraints": [
        {
            "Name": "[CK_Orders_Status]",
            "Expression": "[Status] IN ('Active','Inactive','Pending')"
        }
    ]
}
```

TableLevel style preserves the original constraint names from the database. Use it when constraint naming matters for your deployment process or when you have multi-column check constraints.

If the config specifies a different style than what is already in `Product.json`, SchemaTongs logs a warning and extracts using the style from the existing product file. To change styles on an existing product, update `Product.json` directly.

---

## ScriptDynamicDependencyRemovalForFunctions

When `ShouldCast:ScriptDynamicDependencyRemovalForFunctions` is `true`, SchemaTongs generates a dynamic SQL preamble before each function script. This preamble detects and temporarily removes database objects that depend on the function:

- **Computed columns** that reference the function
- **Check constraints** on columns computed from the function
- **Default constraints** on computed columns referencing the function
- **Foreign keys** involving computed columns that reference the function
- **Indexes** on computed columns or with filter expressions referencing the function

The preamble is inserted as a separate batch (between `GO` statements) before the `CREATE OR ALTER FUNCTION` statement. At deployment time, SchemaQuench executes the preamble first, which drops all dependents, then creates or alters the function. The dependents are re-created by subsequent steps in the quench (table definitions, which restore computed columns, constraints, and indexes).

### When It Matters

SQL Server prevents altering a function when other objects depend on it (computed columns, check constraints, etc.). Without this flag, deploying an updated function requires manually dropping dependents first. Enable this flag when your database has computed columns or constraints that reference user-defined functions.

This flag defaults to `false` because the generated preamble adds complexity to the script and is unnecessary for databases without function dependencies.

### Surgical Use Only

This is a surgical fix, not a default. Enable it on specific functions where the function is referenced by computed columns or filtered indexes AND changes regularly enough that manual dependency management is impractical AND the dependency drop-and-rebuild is acceptably fast.

**The risk that makes this opt-in:** What if the computed column is persisted or indexed on a table with 500 million rows? Dropping that dependency means rebuilding the persisted column or index after the function is updated -- which could take a very long time and impact production availability. The generated preamble does this automatically, every deployment, whether the function actually changed or not.

For functions that rarely change, the right approach is to leave this flag off and write a migration script that carefully handles the dependencies when the function truly needs to change -- with full awareness of the time and performance implications.

---

## Encrypted Object Handling

When SchemaTongs encounters an encrypted object (a function, view, stored procedure, or trigger whose definition in `sys.sql_modules` is `NULL` due to encryption), it:

1. Logs a warning: `WARNING: schema.objectname is encrypted, skipping`
2. Skips the object entirely -- no file is written.
3. Excludes the object from orphan detection so the missing file isn't flagged as an orphan.

Encrypted objects can't be extracted because SQL Server doesn't expose their source text. If you need these objects in your schema package, script them manually and add the files to the appropriate folder.

---

## Excluded Objects

SchemaTongs automatically excludes:

- **System objects** -- Objects flagged as `is_ms_shipped = 1` by SQL Server.
- **Built-in schemas** -- Schema IDs 1-4 (`dbo`, `guest`, `INFORMATION_SCHEMA`, `sys`) and schemas prefixed with `db_` or containing backslashes.
- **SchemaSmith infrastructure** -- All objects in the `SchemaSmith` schema (the helper procedures it deploys).
- **Replication artifacts** -- Tables prefixed with `MSPeer_` or `MSPub_`.
- **Legacy system tables** -- `dtproperties` and `sysdiagrams`.

---

## Environment Variable Mapping

Every configuration key can be overridden with an environment variable using the `SmithySettings_` prefix. Replace `:` separators with double underscores (`__`).

### Complete Mapping

| Configuration Key | Environment Variable |
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
| `ShouldCast:Schemas` | `SmithySettings_ShouldCast__Schemas` |
| `ShouldCast:UserDefinedTypes` | `SmithySettings_ShouldCast__UserDefinedTypes` |
| `ShouldCast:UserDefinedFunctions` | `SmithySettings_ShouldCast__UserDefinedFunctions` |
| `ShouldCast:Views` | `SmithySettings_ShouldCast__Views` |
| `ShouldCast:StoredProcedures` | `SmithySettings_ShouldCast__StoredProcedures` |
| `ShouldCast:TableTriggers` | `SmithySettings_ShouldCast__TableTriggers` |
| `ShouldCast:Catalogs` | `SmithySettings_ShouldCast__Catalogs` |
| `ShouldCast:StopLists` | `SmithySettings_ShouldCast__StopLists` |
| `ShouldCast:DDLTriggers` | `SmithySettings_ShouldCast__DDLTriggers` |
| `ShouldCast:XMLSchemaCollections` | `SmithySettings_ShouldCast__XMLSchemaCollections` |
| `ShouldCast:IndexedViews` | `SmithySettings_ShouldCast__IndexedViews` |
| `ShouldCast:ScriptDynamicDependencyRemovalForFunctions` | `SmithySettings_ShouldCast__ScriptDynamicDependencyRemovalForFunctions` |
| `ShouldCast:ValidateScripts` | `SmithySettings_ShouldCast__ValidateScripts` |
| `ShouldCast:SaveInvalidScripts` | `SmithySettings_ShouldCast__SaveInvalidScripts` |
| `ShouldCast:ObjectList` | `SmithySettings_ShouldCast__ObjectList` |

### Examples

```bash
# Override source connection for CI
export SmithySettings_Source__Server="build-server\\SQLEXPRESS"
export SmithySettings_Source__Database="ProductionDB"
export SmithySettings_Product__Path="/output/MyProduct"

# Disable stored procedure extraction
export SmithySettings_ShouldCast__StoredProcedures="false"

# Set orphan handling to auto-delete
export SmithySettings_OrphanHandling__Mode="DetectDeleteAndCleanup"

# Trust the server certificate
export SmithySettings_Source__ConnectionProperties__TrustServerCertificate="True"
```

---

## Related Documentation

- [Configuration Reference](configuration.md) -- Shared configuration system, CLI switches, settings file format
- [Schema Packages Reference](schema-packages.md) -- Package structure, Product.json, Template.json, folder layout
- [Script Tokens Reference](script-tokens.md) -- Token replacement in SQL scripts
- [Quick Start -- Extract with SchemaTongs](../guide/02-quick-start.md#step-2-extract-with-schematongs) -- Hands-on first extraction
- [Defining Your Schema -- Extracting Changes](../guide/04-defining-your-schema.md#extracting-changes-from-a-live-database) -- Re-extracting after database drift
- [Defining Your Schema -- Extraction Intelligence](../guide/04-defining-your-schema.md#extraction-intelligence) -- Subfolder preservation, orphan detection, script validation
