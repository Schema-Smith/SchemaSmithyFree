# SchemaTongs Reference

Cast your live database into version-controlled code with a single command. SchemaTongs grips every object in your database -- on **SQL Server**, **PostgreSQL**, or **MySQL** -- and extracts them into a clean schema package: tables as JSON, programmable objects as SQL scripts, everything organized and ready to commit to source control. Point it at a database, run it, and you have a deployable package that SchemaQuench can quench onto any compatible target.

The same tool, the same package format, three engines. Whatever your team runs, SchemaTongs casts it.

---

## Installation

**GitHub Releases** -- Download the latest release from the [SchemaSmith releases page](https://github.com/Schema-Smith/SchemaSmith/releases). Unzip and place the executable on your PATH.

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

With a full connection string override (platform-appropriate):

```bash
# SQL Server
SchemaTongs --ConnectionString:"Data Source=myserver;Initial Catalog=NorthwindDev;User ID=sa;Password=secret;TrustServerCertificate=True;"

# PostgreSQL
SchemaTongs --ConnectionString:"Host=myserver;Port=5432;Database=northwind;Username=tongs;Password=secret;"

# MySQL
SchemaTongs --ConnectionString:"Server=myserver;Port=3306;Database=northwind;User=tongs;Password=secret;"
```

SchemaTongs connects to the source database, reads every enabled object type, and writes the results to the configured product path. On first run it creates the full package structure; on subsequent runs it updates existing files in place and **preserves any custom metadata you have attached** under `Extensions`.

### Schema-only mode

If you just need to regenerate the `.json-schemas/*.schema` validation files for an existing product -- without connecting to a database -- use the `--WriteSchemasOnly` switch:

```bash
SchemaTongs --WriteSchemasOnly --Product:Path:./my-product
```

This reads the `Platform` from `Product.json`, regenerates the schema files on the fly from the current engine's C# domain types, and exits. No database connection, no extraction. Useful when you've updated SchemaSmith and want CI validation to match the new engine without re-extracting.

---

## Configuration Reference

SchemaTongs reads configuration from multiple sources, merged in precedence order (highest priority last):

1. **Settings file** -- `SchemaTongs.settings.json` in the working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

For the full explanation of configuration loading, environment variable mapping, and CLI switches, see the [Configuration Reference](configuration.md).

### Complete SchemaTongs.settings.json

```json
{
  "Source": {
    "Server": "",
    "Port": "",
    "User": "",
    "Password": "",
    "Database": "",
    "Platform": "",
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
  "ShouldCast": {
    "Tables": true,
    "Views": true,
    "Functions": true,
    "Procedures": true,
    "TableTriggers": true,
    "ObjectList": "",
    "ValidateScripts": false,
    "SaveInvalidScripts": true,
    "ScriptDynamicDependencyRemovalForFunctions": false,

    "Schemas": true,

    "UserDefinedTypes": true,
    "Catalogs": true,
    "StopLists": true,
    "DDLTriggers": true,
    "XMLSchemaCollections": true,
    "IndexedViews": true,

    "DomainTypes": true,
    "EnumTypes": true,
    "CompositeTypes": true,
    "Aggregates": true,
    "Sequences": true,
    "Rules": true,
    "MaterializedViews": true,

    "Events": true
  },
  "OrphanHandling": {
    "Mode": "Detect"
  },
  "FolderMapping": {
    "Schemas": "Schemas",
    "Functions": "Functions",
    "Views": "Views",
    "Procedures": "Procedures",
    "Triggers": "Triggers"
  }
}
```

The platform-specific `ShouldCast` flags are simply ignored when they don't apply to the configured `Source:Platform`. You can leave the full set in place across all your environments.

### Source connection

| Key | Type | Default | Description |
|---|---|---|---|
| `Source:Platform` | string | _(required)_ | One of `"SqlServer"`, `"PostgreSQL"`, or `"MySQL"`. Determines which extraction adapter runs. |
| `Source:Server` | string | _(required)_ | Database server hostname or IP. |
| `Source:Port` | string | platform default | TCP port. SQL Server `1433`, PostgreSQL `5432`, MySQL `3306`. |
| `Source:User` | string | _(empty)_ | Login username. SQL Server allows blank for Windows authentication. |
| `Source:Password` | string | _(empty)_ | Login password. |
| `Source:Database` | string | _(required)_ | Name of the database to extract from. |
| `Source:ConnectionProperties` | object | `{}` | Arbitrary key-value pairs appended to the connection string. Platform-specific keys -- see the [Configuration Reference](configuration.md#connection-configuration). |

The `--ConnectionString` switch bypasses all `Source` settings entirely. When provided, `Server`, `Port`, `User`, `Password`, `Database`, and `ConnectionProperties` are all ignored. `Source:Platform` is still required so the right adapter can claim the connection.

### Product output

| Key | Type | Default | Description |
|---|---|---|---|
| `Product:Path` | string | _(required)_ | Directory where the schema package is created or updated. |
| `Product:Name` | string | _(directory name)_ | Product name written to `Product.json`. If blank, defaults to the last segment of `Product:Path`. |
| `Product:CheckConstraintStyle` | string | `ColumnLevel` | Controls how check constraints are written when creating a new `Product.json`. See [CheckConstraintStyle](#checkconstraintstyle). |
| `Template:Name` | string | Source database name | Template name. Creates the template directory under `Templates/<Name>/`. Defaults to the `Source:Database` value when not specified. |

---

## ShouldCast Flags

Control exactly what gets cast from the database. Each object type can be individually enabled or disabled. Flags that don't apply to the active platform are simply ignored.

### All platforms

| Flag | Default | What It Extracts |
|---|---|---|
| `Tables` | `true` | Table definitions as JSON files |
| `Views` | `true` | View definitions as SQL scripts |
| `Functions` | `true` | User-defined functions |
| `Procedures` | `true` | Stored procedures |
| `TableTriggers` | `true` | Table-level triggers |
| `ObjectList` | _(empty)_ | Comma- or semicolon-separated list of specific objects to extract. See [ObjectList Filtering](#objectlist-filtering). |
| `ValidateScripts` | `false` | Parses each extracted SQL script for validity. See [Script Validation](#script-validation). |
| `SaveInvalidScripts` | `true` | When `ValidateScripts` is enabled, saves failing scripts as `.sqlerror` files. |
| `ScriptDynamicDependencyRemovalForFunctions` | `false` | Generates a dynamic dependency-removal preamble for function scripts. See [ScriptDynamicDependencyRemovalForFunctions](#scriptdynamicdependencyremovalforfunctions). |

### SQL Server and PostgreSQL

| Flag | Default | What It Extracts |
|---|---|---|
| `Schemas` | `true` | Schema creation scripts (MySQL doesn't have schemas distinct from databases) |

### SQL Server only

| Flag | Default | What It Extracts |
|---|---|---|
| `UserDefinedTypes` | `true` | User-defined data types and table types |
| `Catalogs` | `true` | Full-text catalogs |
| `StopLists` | `true` | Full-text stop lists with their stop words |
| `DDLTriggers` | `true` | Database-level DDL triggers |
| `XMLSchemaCollections` | `true` | XML schema collections |
| `IndexedViews` | `true` | Indexed (materialized) view definitions as JSON files |

### PostgreSQL only

| Flag | Default | What It Extracts |
|---|---|---|
| `DomainTypes` | `true` | `CREATE DOMAIN` user-defined types |
| `EnumTypes` | `true` | `CREATE TYPE ... AS ENUM` types |
| `CompositeTypes` | `true` | `CREATE TYPE ... AS (...)` composite types |
| `Aggregates` | `true` | User-defined aggregate functions |
| `Sequences` | `true` | Sequence objects |
| `Rules` | `true` | Rules attached to tables and views |
| `MaterializedViews` | `true` | Materialized view definitions as JSON files |

### MySQL only

| Flag | Default | What It Extracts |
|---|---|---|
| `Events` | `true` | MySQL scheduled events |

---

## Folder Mapping

By default, each extracted object type lands in the standard folder for the active platform (see [Schema Packages -- Default Folders](schema-packages.md#default-folders)). The optional `FolderMapping` section lets you rename those default folders without breaking SchemaQuench's recognition of them.

```json
{
  "FolderMapping": {
    "Schemas": "01_Schemas",
    "Functions": "02_Functions",
    "Views": "03_Views",
    "Procedures": "04_Procedures",
    "Triggers": "05_Triggers"
  }
}
```

The keys are `ScriptObjectType` values (e.g., `Schemas`, `Functions`, `Views`, `Procedures`, `Triggers`, `DomainTypes`, `EnumTypes`, `CompositeTypes`, `Sequences`, `Rules`, `Aggregates`, `Events`, etc.). The values are the folder names you want to use under each template directory.

A few rules:

- **Fixed folders cannot be remapped.** `IndexedViews` (SQL Server) and `MaterializedViews` (PostgreSQL) always live in `Indexed Views/` and `Materialized Views/`. Tables always live in `Tables/`.
- **Duplicate folder names are rejected.** SchemaTongs validates the mapping at startup; if two object types map to the same folder, you get an error before extraction begins.
- **Unmapped types use the platform default.** Anything you leave out of `FolderMapping` keeps its standard folder name.

This pairs naturally with the `ScriptFolders` array on `Template.json`. You can extract using your renamed folder layout and SchemaQuench will still pick up the right slot for each folder.

---

## ObjectList Filtering

The `ObjectList` setting restricts extraction to a specific set of objects. Provide a comma- or semicolon-separated list of object names:

```json
"ShouldCast": {
  "ObjectList": "dbo.Customer,dbo.vw_ActiveCustomers,usp_GetOrders"
}
```

Names can be specified with or without a schema prefix. Matching is case-insensitive. When `ObjectList` is empty (the default), all objects matching the enabled `ShouldCast` flags are extracted.

When `ObjectList` is active, **orphan detection is automatically disabled** -- SchemaTongs can't tell which files are genuinely orphaned when only a subset of objects is being extracted.

---

## Orphan Detection

Databases change. Objects get dropped or renamed. The old script files linger in your package -- unless you tell SchemaTongs what to do about them. Configure orphan behavior with `OrphanHandling:Mode`:

| Mode | Behavior |
|---|---|
| `Detect` | Orphaned files are logged as warnings. No files are modified or deleted. **Default.** |
| `DetectWithCleanupScripts` | Orphaned files are logged. For each orphan in a script folder, a cleanup script containing a `DROP` statement is generated as `_OrphanCleanup_<FolderName>.sql` in the log directory. The orphaned files themselves are left in place. |
| `DetectDeleteAndCleanup` | Orphaned files are deleted from the package. Cleanup `DROP` scripts are generated in the log directory. |

### When to use each mode

- **`Detect`** -- Safe default. Use during routine re-extraction when you want to review orphans manually before taking action.
- **`DetectWithCleanupScripts`** -- Use when you want SchemaTongs to prepare the cleanup work but you want to review the generated scripts and delete orphaned files yourself.
- **`DetectDeleteAndCleanup`** -- Use when you trust the extraction to be authoritative and want a fully clean package after each run. Particularly useful in automated pipelines.

### How it works

SchemaTongs builds a file index for each extraction folder before extraction begins. As objects are extracted and written to disk, each file is marked as "written." After extraction completes, any indexed file that wasn't written is an orphan. Cleanup scripts from previous runs are archived into numbered `SchemaTongs.NNNN` backup directories before new ones are generated.

Orphan detection only runs for object types that were fully extracted (`ShouldCast` flag enabled and no `ObjectList` filter active).

### The core tension

An "orphan" might be a script for an object that was genuinely removed from the database, OR it might be a new object you added to the package that has not been deployed yet. Deleting it automatically would destroy your pending work. The default (`Detect`) is conservative because it never destroys anything -- it just tells you.

---

## Custom Property Preservation

Custom metadata you've attached to your tables under [`Extensions`](custom-properties.md) is preserved across re-extractions. When SchemaTongs writes a table file, it checks whether a previous file existed for the same table and copies the previous `Extensions` content forward onto the freshly extracted table.

The preservation pass uses the component's `Name` (with quote/bracket characters stripped, case-insensitive). For columns and the table itself, matching also falls back to `OldName`, so renamed components keep their custom metadata as long as you set `OldName` correctly before the rename is deployed.

| Component | Matched By | All Platforms |
|---|---|---|
| Table | root object | Yes |
| Column | `Name`, then `OldName` | Yes |
| Index | `Name` | Yes |
| ForeignKey | `Name` | Yes |
| CheckConstraint | `Name` | Yes |
| ExcludeConstraint | `Name` | PostgreSQL only |
| Statistic | `Name` | PostgreSQL only |
| FullTextIndex | `Name` | SQL Server, MySQL |
| MaterializedView | root object | PostgreSQL only |
| IndexedView | root object | SQL Server only |
| XmlIndex | `Name` | SQL Server only |

This is what makes [Custom Properties](custom-properties.md) trustworthy across the SchemaTongs / SchemaQuench round-trip: you attach metadata once, and it survives every re-extraction as long as the underlying object stays.

---

## Script Validation

Catch problems at extraction time instead of discovering them during deployment. When `ShouldCast:ValidateScripts` is `true`, SchemaTongs tests each extracted SQL script for validity immediately after writing it. The validation strategy depends on the platform and the object type, but the principle is the same: try to compile the object server-side, roll back, and flag failures.

### Invalid script handling

Scripts that fail validation are handled according to `ShouldCast:SaveInvalidScripts`:

| SaveInvalidScripts | Behavior |
|---|---|
| `true` (default) | The script is saved with a `.sqlerror` extension instead of `.sql`. The original `.sql` file (if any) is removed. |
| `false` | The script isn't written to disk at all. |

### .sqlerror files

`.sqlerror` files are SQL scripts that failed extraction validation. They serve as a record of potential problems without blocking the rest of the package.

| Tool | Behavior |
|---|---|
| **SchemaQuench** | Skips `.sqlerror` files -- only `.sql` files are loaded and executed. |
| **SchemaTongs** | On re-extraction, overwrites `.sqlerror` files with the latest content. If the script still fails validation, it stays as `.sqlerror`. If it now passes, it's written as `.sql` and the `.sqlerror` is removed. |

### False positives

Validation failures aren't always genuine errors. Common false positives include:

- **Cross-database references** -- Scripts referencing objects in another database may fail when the parser can't resolve the cross-database context.
- **Temporary objects** -- References to temp tables or variables created elsewhere in the same batch.
- **Forward references inside the same batch** -- A function that calls another function created later in the same script.

To override a false positive, rename the file from `.sqlerror` to `.sql`. SchemaQuench will then include it in the next deployment.

When any scripts fail validation, SchemaTongs generates an `_InvalidObjectCleanup.sql` file in the log directory containing diagnostic information for all invalid objects detected during the extraction run.

---

## Subfolder Preservation

Organize your scripts however makes sense for your team -- SchemaTongs respects that structure across re-extractions. Any extracted script folder supports user-created subfolders for organizing scripts. For example, you might organize stored procedures by domain:

```
Procedures/
  Orders/
    public.usp_get_order.sql
    public.usp_create_order.sql
  Customers/
    public.usp_get_customer.sql
```

SchemaTongs preserves these subfolders across re-extraction. Before extraction begins, it builds a file index that maps every `.sql`, `.sqlerror`, and `.json` file to its full path, including any subfolder. When writing an extracted object, SchemaTongs checks the index:

- If the file already exists in a subfolder, it's written back to that same subfolder.
- If the file exists in multiple subfolders, a warning is logged and the file is written to the folder root.
- If the file is new (not in the index), it's written to the folder root.

New objects always appear in the root of their folder. Move them into subfolders as desired -- SchemaTongs will remember the location on the next run. This means you can shape your extracted package to fit *your* code review and team conventions, not the other way around.

---

## Package Initialization

The first extraction is where your schema package is born. When SchemaTongs runs against a path that doesn't yet contain a schema package, it creates the full structure:

1. Creates the product directory at `Product:Path`.
2. Generates `Product.json` with the configured product name and the `Platform` from `Source:Platform`.
3. Creates the template directory under `Templates/<TemplateName>/`.
4. Generates `Template.json` with a `DatabaseIdentificationScript` targeting the source database.
5. Creates all standard script folders for the active platform (see [Default Folders](schema-packages.md#default-folders)).
6. Creates a `.json-schemas/` directory with JSON Schema validation files generated **on the fly** from the live engine types.

On subsequent runs against an existing package, SchemaTongs overwrites object scripts and table definitions with the current database state. It does not modify `Product.json` or `Template.json` -- if you change `Platform`, `Name`, or `TemplateOrder` after the first extraction, those edits stick.

### Helper procedures

On every run, SchemaTongs deploys (or updates) lightweight helper procedures in the source database under the `SchemaSmith` schema. These procedures generate the JSON representation of tables and views and are used internally by the extraction adapter. They're read-only, schema-prefixed, and excluded from extraction output.

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

`TableLevel` style preserves the original constraint names from the database. Use it when constraint naming matters for your deployment process or when you have multi-column check constraints.

If the config specifies a different style than what is already in `Product.json`, SchemaTongs logs a warning and extracts using the style from the existing product file. To change styles on an existing product, update `Product.json` directly.

---

## ScriptDynamicDependencyRemovalForFunctions

When `ShouldCast:ScriptDynamicDependencyRemovalForFunctions` is `true`, SchemaTongs generates a dynamic SQL preamble before each function script. This preamble detects and temporarily removes database objects that depend on the function (computed columns, check constraints, foreign keys involving computed columns, indexes on computed columns or filter expressions referencing the function). At deployment time, SchemaQuench executes the preamble first, dropping all dependents, then creates or alters the function. The dependents are recreated by subsequent steps in the quench (table definitions restore computed columns, constraints, and indexes).

### Surgical use only

This is a surgical fix, not a default. Enable it on functions where:

1. The function is referenced by computed columns or filtered indexes, AND
2. Changes regularly enough that manual dependency management is impractical, AND
3. The dependency drop-and-rebuild is acceptably fast.

**The risk that makes this opt-in:** if a computed column is persisted or indexed on a 500-million-row table, dropping that dependency means rebuilding the persisted column or index after the function is updated -- which could take a very long time and impact production availability. For functions that rarely change, the right approach is to leave this flag off and write a migration script that handles the dependencies carefully when the function truly needs to change.

---

## Encrypted Object Handling (SQL Server)

When SchemaTongs encounters an encrypted object on SQL Server (a function, view, stored procedure, or trigger whose definition in `sys.sql_modules` is `NULL`), it logs a warning, skips the object, and excludes it from orphan detection. Encrypted objects can't be extracted because the engine doesn't expose their source text. If you need these objects in your schema package, script them manually and add the files to the appropriate folder.

---

## Excluded Objects

SchemaTongs automatically excludes the platform's system schemas and internal infrastructure:

- **System objects** -- Anything flagged as system-shipped by the source engine.
- **Built-in schemas** -- `dbo`, `guest`, `INFORMATION_SCHEMA`, `sys` (SQL Server); `pg_*`, `information_schema` (PostgreSQL); `mysql`, `information_schema`, `performance_schema`, `sys` (MySQL).
- **SchemaSmith infrastructure** -- All objects in the `SchemaSmith` schema (the helper procedures SchemaTongs and SchemaQuench deploy).
- **Replication artifacts** (SQL Server) -- Tables prefixed with `MSPeer_` or `MSPub_`.
- **Legacy system tables** (SQL Server) -- `dtproperties` and `sysdiagrams`.

---

## Related Documentation

- [Configuration Reference](configuration.md) -- Shared configuration system, CLI switches, settings file format, environment variable mapping
- [Schema Packages Reference](schema-packages.md) -- Package structure, default folders, JSON formats
- [Custom Properties](custom-properties.md) -- The `Extensions` carrier and how it's preserved through re-extraction
- [SchemaQuench Reference](schemaquench.md) -- Deployment tool that consumes the packages SchemaTongs creates
- [DataTongs Reference](datatongs.md) -- Reference data extraction
