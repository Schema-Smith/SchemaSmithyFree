# Chapter 7 -- Troubleshooting

When something goes wrong, let's figure out what happened. This chapter helps you find the answer fast. Issues are organized by symptom so you can jump directly to what you are seeing.

For background on how the tools work, see the individual reference pages: [SchemaTongs](../reference/schematongs.md), [SchemaQuench](../reference/schemaquench.md), [SchemaHammer](../reference/schemahammer.md), [DataTongs](../reference/datatongs.md), and [Configuration](../reference/configuration.md).

---

## Reading Logs

Every SchemaSmith CLI tool writes two log files during each run. These are the first place to look when something does not go as expected:

- **Progress log** (`ToolName - Progress.log`) -- a step-by-step record of what the tool did. Start here.
- **Error log** (`ToolName - Errors.log`) -- detailed exception information when something fails. Check this for stack traces and SQL error details.

Logs are written to the tool's working directory by default. You can redirect them with `--LogPath`:

```
SchemaQuench --LogPath:/var/log/schemasmith
```

### Numbered backup directories

After each run, the tool copies its logs into a numbered backup directory (e.g., `SchemaQuench.0001/`, `SchemaQuench.0002/`). This preserves the history of previous runs so you can compare what changed between deployments. When you are tracking down a regression, these numbered backups are your timeline.

### Password masking in logs

The progress log records your full configuration at the start of each run, but any setting whose name contains "Password" or "Pwd" is masked as `**********`. If you see asterisks where you expected credentials, that is the masking working correctly -- the actual password was still used for the connection.

---

## Exit Codes

Each tool exits with a code that indicates the outcome. Automation scripts should check this value.

| Code | Meaning | What to do |
|------|---------|------------|
| 0 | Success | Nothing -- the run completed normally. |
| 2 | One or more database quenches failed | Check the progress log for `FAILED to quench` messages. The error log has details. Common causes: SQL errors in scripts, dependency failures, connection drops. |
| 3 | Unhandled exception | An unexpected error crashed the tool. Check the error log for the full stack trace. This usually indicates a bug or an environment issue (missing files, permission denied). |
| 4 | Unable to back up log files | The tool completed (or failed) but could not copy its logs to the backup directory. Check directory permissions and disk space. |

For the full exit code reference, see [Configuration Reference -- Exit Codes](../reference/configuration.md#exit-codes).

---

## Common Deployment Issues (SchemaQuench)

### "Invalid object name 'SchemaSmith.*'"

**Symptom:** SQL errors referencing `SchemaSmith.QuenchTables`, `SchemaSmith.MissingTableAndColumnQuench`, or similar objects.

**Cause:** The SchemaSmith stored procedures have not been installed in the target database. These procedures are created by the KindleTheForge step at the start of each deployment.

**Fix:** Ensure `KindleTheForge` is set to `true` (the default) in your settings. If you explicitly set it to `false`, the tool skips installing the forge procedures, and the database will not have the objects it needs.

```json
{
  "KindleTheForge": true
}
```

### Dependency failures that do not resolve

**Symptom:** Scripts fail with errors like "Invalid object name" or "Cannot find the object" even though the referenced object is in your package. The progress log shows the same scripts failing on every retry pass.

**Cause:** SchemaQuench retries object scripts in a dependency retry loop -- each pass attempts all unquenched scripts, and the loop continues as long as at least one new script succeeds per pass. Objects-slot scripts get four opportunities to resolve across the deployment sequence (see [SchemaQuench Reference -- Dependency Retry Loop](../reference/schemaquench.md#dependency-retry-loop)). If scripts have circular dependencies, or depend on objects that genuinely do not exist, the retry loop cannot make progress.

**Fix:**
1. Check the progress log for the specific scripts that failed and the SQL errors they produced.
2. Look for circular dependencies between views, functions, or procedures. True circular dependencies cannot be resolved by retries -- you need to break the cycle (e.g., use a stub object that the second pass updates).
3. Verify the referenced object actually exists in your schema package. A typo in a schema or object name will fail on every pass.
4. If the failure is in the table-creation boundary (object references a table column that doesn't exist yet), the retry loop should resolve it automatically across passes. If not, check whether the table JSON is valid.

### Foreign key errors during deployment

**Symptom:** Foreign key creation fails because the referenced table or column does not exist, or data violates the constraint.

**Cause:** Foreign keys are applied after table modifications. If you need to run data migration scripts between table changes and foreign key creation, the `BetweenTablesAndKeys` migration slot is exactly the right tool for the job.

**Fix:** Place your data fixup or migration scripts in the `MigrationScripts/BetweenTablesAndKeys/` folder of your template. These run after tables and columns are updated but before foreign keys and constraints are applied. See [Edge Cases -- Migration Scripts](10-edge-cases.md#migration-scripts) for practical guidance, and the [SchemaQuench Reference](../reference/schemaquench.md#execution-slots) for the full deployment order.

### Validation script returns false

**Symptom:** The progress log shows "Validate Server" followed by "Invalid server for this product" and the deployment stops.

**Cause:** Your `Product.ValidationScript` ran against the target server and returned a value that is not `true`. This is the safety gate working as designed — it prevents quenching to the wrong server.

**Fix:** Check the SQL in your `Product.json` `ValidationScript` field. Run it manually against the target server to see what it returns. Common issues:
- The script checks for a specific server name or database that does not exist on this target.
- The script has a logic error that causes it to return `false` or `NULL` (NULL is treated as false).

### Connection failures

**Symptom:** The progress log shows `**CONNECTION FAILED**` and the error log contains a connection exception.

**Cause:** SchemaQuench could not connect to the target SQL Server.

**Fix:** Let's walk through the connection settings:
- `Target:Server` -- the server hostname or IP address
- `Target:Port` -- if SQL Server is not on the default port
- `Target:User` and `Target:Password` -- if using SQL authentication
- `Target:ConnectionProperties` -- check `TrustServerCertificate` if you see certificate-related errors

```json
{
  "Target": {
    "Server": "localhost",
    "Port": "1433",
    "User": "sa",
    "Password": "YourPassword",
    "ConnectionProperties": {
      "TrustServerCertificate": "true"
    }
  }
}
```

If using Windows authentication, omit `User` and `Password` entirely.

### WhatIf shows unexpected changes

**Symptom:** Running with `WhatIfONLY: true` shows changes you did not expect -- tables being modified, columns being added or dropped.

**Cause:** The live database has drifted from what the schema package defines. WhatIf is showing you the delta between your package and the actual database state. This is WhatIf doing exactly what it should.

**Fix:**
1. Compare your schema package against the live database to identify what drifted.
2. If someone made manual changes to the database, decide whether to update your package (cast with SchemaTongs, as described in [Defining Your Schema -- Extracting Changes](04-defining-your-schema.md#extracting-changes-from-a-live-database)) or let SchemaQuench bring the database back in line.
3. If your package has unexpected definitions, check for uncommitted changes or the wrong package version.

---

## Common Extraction Issues (SchemaTongs)

### Encrypted objects warning

**Symptom:** The progress log shows `WARNING: [schema].[object] is encrypted, skipping`.

**Cause:** SQL Server objects created with `WITH ENCRYPTION` cannot have their source code retrieved. This is a SQL Server limitation, not a SchemaTongs issue.

**Fix:** No action needed if you expect these objects to be encrypted. If you need to manage them through SchemaSmith, they must be recreated without encryption.

### Objects not appearing in extraction output

**Symptom:** You know an object exists in the database, but SchemaTongs did not cast it.

**Cause:** SchemaTongs filters extraction based on two settings:

1. **ShouldCast flags** -- Each object type has a flag (`ShouldCast:Views`, `ShouldCast:StoredProcedures`, etc.) that defaults to `true`. If set to `false`, that entire category is skipped.
2. **ObjectList filter** -- If `ShouldCast:ObjectList` is set, only the explicitly listed objects are extracted.

**Fix:** Check your settings file:
- Verify the `ShouldCast` flag for the object type is not set to `false`.
- If you are using `ObjectList`, make sure the object is included in the comma-separated list.
- Remember that `ObjectList` takes a comma- or semicolon-separated list of object names (case-insensitive).

See [SchemaTongs Reference](../reference/schematongs.md) for the full list of ShouldCast flags.

### Orphan warnings

**Symptom:** The progress log shows "orphaned file(s)" detected in one or more folders.

**Cause:** SchemaTongs found `.sql` files in the template directory that do not correspond to any object in the live database. This usually means the object was dropped or renamed in the database since the last extraction.

**Fix:** Review the listed files:
- If the objects were intentionally removed from the database, the orphan files should be cleaned up. Set `OrphanHandling:Mode` to `DetectWithCleanupScripts` to generate DROP scripts, or `DetectDeleteAndCleanup` to also delete the orphan files.
- If the objects should still exist, investigate why they are missing from the database.
- Orphan detection is skipped when `ObjectList` is active, since a partial extraction cannot determine what is truly orphaned.

### Script validation errors (.sqlerror files)

**Symptom:** Some extracted files have a `.sqlerror` extension instead of `.sql`, and SchemaHammer shows them with an error icon.

**Cause:** When `ShouldCast:ValidateScripts` is enabled, SchemaTongs checks each extracted script for syntax validity. Scripts that fail validation are saved with the `.sqlerror` extension.

**Fix:**
1. Open the `.sqlerror` file to see the raw extracted content and understand what went wrong.
2. Common causes: the object depends on other objects that do not exist in the validation context, or the object uses syntax that the parser cannot validate in isolation.
3. If the scripts are actually valid (false positives from isolated validation), you can set `ShouldCast:SaveInvalidScripts` to `false` to discard them, or disable validation with `ShouldCast:ValidateScripts: false`.

---

## Common DataTongs Issues

### "Could not determine key columns"

**Symptom:** The progress log shows a message like "Table [name] has no primary key or unique index and no KeyColumns configured. Skipping table."

**Cause:** DataTongs generates MERGE statements that need a key to match source and target rows. It looks for a primary key first, then a unique index. If neither exists, it cannot proceed.

**Fix:** Specify key columns manually in your DataTongs configuration:

```json
{
  "Tables": [
    {
      "Name": "dbo.MyTable",
      "KeyColumns": "[Column1],[Column2]"
    }
  ]
}
```

If a key column is nullable, prefix it with `*` to generate NULL-safe comparisons: `"KeyColumns": "*[NullableCol],[NonNullableCol]"`.

See [DataTongs Reference](../reference/datatongs.md) for details.

### Column types excluded from output

**Symptom:** Certain columns are missing from the generated MERGE script.

**Cause:** DataTongs automatically excludes columns of type `sql_variant`, `rowversion`, and `timestamp` because these types cannot be round-tripped through JSON serialization. Computed columns and `rowguidcol` columns are also excluded.

**Fix:** This is expected behavior. If you need data from these columns, you will need to handle them with custom scripts outside of DataTongs.

### Empty output (no script generated)

**Symptom:** DataTongs runs without errors but says "No data found -- skipping script generation" for a table.

**Cause:** The table is empty in the source database, or your `Filter` expression excludes all rows.

**Fix:**
- Verify the table has data in the source database.
- If you specified a `Filter`, run the equivalent `WHERE` clause against the source to confirm it matches rows.
- Check that you are connecting to the correct source database (`Source:Database` in settings).

### Table does not exist in source database

**Symptom:** The progress log shows "Table [schema].[name] does not exist in source database. Skipping table."

**Cause:** The table name in your configuration does not match any table in the source database.

**Fix:** Check the table name for typos. DataTongs expects the format `schema.tablename` (e.g., `dbo.Products`). If the schema is omitted, `dbo` is assumed.

---

## SchemaHammer Issues

### Product will not open

**Symptom:** SchemaHammer shows an error dialog when you try to open a Product.

**Cause:** The `Product.json` file is missing, has invalid JSON, or the path is incorrect.

**Fix:**
1. Verify the `Product.json` file exists at the expected path.
2. Open it in a text editor and check for JSON syntax errors (missing commas, unmatched braces).
3. If the file was corrupted, cast with SchemaTongs to regenerate it.

### Token preview not resolving

**Symptom:** Script tokens like `{{DatabaseName}}` appear literally in the preview instead of being replaced with values.

**Cause:** The token is not defined in either `Product.json` or `Template.json` `ScriptTokens`.

**Fix:**
- Check `ScriptTokens` in your `Product.json` -- product-level tokens apply to all templates.
- Check `ScriptTokens` in the relevant `Template.json` -- template-level tokens apply to that template only.
- Token names are case-insensitive. Verify the spelling matches between the script and the token definition. See the [Script Tokens Reference](../reference/script-tokens.md) for the full resolution order.
- `{{DatabaseName}}` is a built-in token that resolves at deployment time, not in the editor preview. This is expected for runtime tokens.

### Tree nodes missing or empty

**Symptom:** You expanded a folder in the tree but it appears empty, or certain nodes are not showing.

**Cause:** SchemaHammer uses lazy loading -- child nodes load when you expand their parent. If the underlying folder is empty on disk, the node will have no children.

**Fix:**
- Expand the parent node to trigger loading.
- Verify the folder on disk actually contains `.sql` files.
- If you just ran SchemaTongs, the files should be in the template directory structure (e.g., `Tables/`, `Views/`, `Procedures/`).

---

## Environment and Platform Issues

### Cross-platform path issues

**Symptom:** Paths work on one operating system but fail on another.

**Fix:** Use forward slashes (`/`) in configuration files. SchemaSmith normalizes paths internally, but forward slashes are valid on all platforms (Windows, macOS, Linux).

```json
{
  "Product": {
    "Path": "./my-product"
  },
  "OutputPath": "./output/data"
}
```

Avoid backslashes in JSON configuration -- they require escaping (`\\`) and reduce portability.

### Docker SQL Server not responding

**Symptom:** Connection failures when targeting a SQL Server running in Docker.

**Fix:**
1. Verify the container is running: `docker ps`
2. Check the port mapping -- the default SQL Server port is 1433. If you mapped it differently, specify it in `Target:Port`.
3. Confirm the SA password meets SQL Server complexity requirements.
4. Add `TrustServerCertificate: true` to your connection properties, since Docker containers typically use self-signed certificates.

```json
{
  "Target": {
    "Server": "localhost",
    "Port": "1433",
    "User": "sa",
    "Password": "YourStrong!Passw0rd",
    "ConnectionProperties": {
      "TrustServerCertificate": "true"
    }
  }
}
```

### Environment variables not taking effect

**Symptom:** You set an environment variable but the tool does not use the value.

**Cause:** SchemaSmith environment variables require a specific prefix and separator format.

**Fix:**
- Prefix all variables with `SmithySettings_`.
- Use double underscores (`__`) to represent hierarchy levels.
- Restart your shell after setting variables (or use `export` in the current session).

Examples:
```bash
export SmithySettings_Target__Server=my-server
export SmithySettings_Target__User=sa
export SmithySettings_Target__Password=secret
```

These map to `Target:Server`, `Target:User`, and `Target:Password` in the configuration hierarchy. See [Configuration Reference](../reference/configuration.md) for the full precedence rules.

---

## Still Stuck?

If your issue is not covered here, check the [reference documentation](../README.md#reference) for detailed behavior descriptions, or open an issue on [GitHub](https://github.com/Schema-Smith/SchemaSmithyFree/issues).

If you're still stuck and want to talk it through, reach out to Forge directly — [ForgeBarrett@SchemaSmith.com](mailto:ForgeBarrett@SchemaSmith.com). That's real developers on the other end, and we're happy to help.

---

This is the final chapter of the guide. For a refresher on the basics, head back to [Why SchemaSmith](01-why-schemasmith.md) or jump straight to the [Quick Start](02-quick-start.md).
