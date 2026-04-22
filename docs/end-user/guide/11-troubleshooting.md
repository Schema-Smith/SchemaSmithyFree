# Troubleshooting

When something goes wrong, let's figure out what happened. This chapter helps you find the answer fast. Issues are organized by symptom so you can jump directly to what you're seeing.

For background on how the tools work, see the individual reference pages: [SchemaTongs](../reference/schematongs.md), [SchemaQuench](../reference/schemaquench.md), [DataTongs](../reference/datatongs.md), [Custom Properties](../reference/custom-properties.md), [Script Tokens](../reference/script-tokens.md), and [Configuration](../reference/configuration.md).

---

## Reading logs

Every SchemaSmith CLI tool writes two log files during each run. These are the first place to look when something doesn't go as expected:

- **Progress log** (`ToolName - Progress.log`) -- a step-by-step record of what the tool did. Start here.
- **Error log** (`ToolName - Errors.log`) -- detailed exception information when something fails. Check this for stack traces and SQL error details.

Logs are written to the tool's working directory by default. You can redirect them with `--LogPath`:

```
SchemaQuench --LogPath:/var/log/schemasmith
```

### Numbered backup directories

After each run, the tool copies its logs into a numbered backup directory (e.g., `SchemaQuench.0001/`, `SchemaQuench.0002/`). This preserves the history of previous runs so you can compare what changed between deployments. When you're tracking down a regression, these numbered backups are your timeline.

### Password masking in logs

The progress log records your full configuration at the start of each run, but any setting whose name contains "Password" or "Pwd" is masked as `**********`. If you see asterisks where you expected credentials, that's the masking working correctly -- the actual password was still used for the connection.

---

## Exit codes

Each tool exits with a code that indicates the outcome. Automation scripts should check this value.

| Code | Meaning | What to do |
|------|---------|------------|
| 0 | Success | Nothing -- the run completed normally. |
| 2 | One or more database quenches failed | Check the progress log for `FAILED to quench` messages. The error log has details. |
| 3 | Unhandled exception | An unexpected error crashed the tool. Check the error log for the full stack trace. |
| 4 | Unable to back up log files | The tool completed (or failed) but couldn't copy its logs to the backup directory. Check directory permissions and disk space. |

For the full exit code reference, see [Configuration Reference -- Exit Codes](../reference/configuration.md#exit-codes).

---

## Common deployment issues (SchemaQuench)

### "Invalid object name 'SchemaSmith.*'"

**Symptom:** SQL errors referencing `SchemaSmith.MissingTableAndColumnQuench`, `SchemaSmith.ForeignKeyQuench`, or similar objects.

**Cause:** The SchemaSmith helper procedures haven't been installed in the target database. These are created by the KindleTheForge step at the start of each deployment.

**Fix:** Ensure `KindleTheForge` is set to `true` (the default) in your settings. If you explicitly set it to `false`, the tool skips installing the forge procedures, and the database won't have the objects it needs.

```json
{ "KindleTheForge": true }
```

### Dependency failures that don't resolve

**Symptom:** Scripts fail with errors like "Invalid object name", "relation ... does not exist", or "Unknown column" even though the referenced object is in your package. The progress log shows the same scripts failing on every retry pass.

**Cause:** SchemaQuench retries object scripts in a dependency retry loop -- each pass attempts all unquenched scripts, and the loop continues as long as at least one new script succeeds per pass. Objects-slot scripts get four opportunities to resolve across the deployment sequence. If scripts have circular dependencies, or depend on objects that genuinely do not exist, the retry loop cannot make progress.

**Fix:**

1. Check the progress log for the specific scripts that failed and the SQL errors they produced.
2. Look for circular dependencies between views, functions, or procedures. True circular dependencies can't be resolved by retries -- you need to break the cycle (e.g., use a stub object that the second pass updates).
3. Verify the referenced object actually exists in your schema package. A typo in a schema or object name will fail on every pass.
4. If the failure is in the table-creation boundary (object references a table column that doesn't exist yet), the retry loop should resolve it automatically across passes. If not, check whether the table JSON is valid.

### Foreign key errors during deployment

**Symptom:** Foreign key creation fails because the referenced table or column doesn't exist, or data violates the constraint.

**Cause:** Foreign keys are applied after table modifications. If you need to run data migration scripts between table changes and foreign key creation, the `BetweenTablesAndKeys` migration slot is exactly the right tool for the job.

**Fix:** Declare a custom folder in the `BetweenTablesAndKeys` slot via `Template.ScriptFolders` and put your data fixup or migration scripts there. See [Edge Cases -- Migration Scripts](10-edge-cases.md#migration-scripts) and the [SchemaQuench Reference](../reference/schemaquench.md#database-quench-sequence) for the full sequence.

### Validation script returns false

**Symptom:** The progress log shows `Validate Server` followed by `Invalid server for this product` and the deployment stops.

**Cause:** Your `Product.ValidationScript` ran against the target server and returned a value that isn't truthy. This is the safety gate working as designed -- it prevents quenching to the wrong server.

**Fix:** Check the SQL in your `Product.json` `ValidationScript` field. Run it manually against the target server to see what it returns. Common issues:

- The script checks for a specific server name or database that doesn't exist on this target.
- The script has a logic error that causes it to return `false` or `NULL` (NULL is treated as false).

### Connection failures

**Symptom:** The progress log shows `**CONNECTION FAILED**` and the error log contains a connection exception.

**Cause:** SchemaQuench couldn't connect to the target server.

**Fix:** Walk through the connection settings:

- `Target:Server` -- the server hostname or IP address
- `Target:Port` -- if the server isn't on the platform's default port (SQL Server `1433`, PostgreSQL `5432`, MySQL `3306`)
- `Target:User` and `Target:Password` -- credentials (SQL Server allows blank for Windows auth; PostgreSQL and MySQL do not)
- `Target:ConnectionProperties` -- platform-specific keys (`TrustServerCertificate` on SQL Server, `SslMode` on PostgreSQL / MySQL)

If using SQL Server Windows authentication, omit `User` and `Password` entirely. For PostgreSQL and MySQL, always provide explicit credentials.

### WhatIf shows unexpected changes

**Symptom:** Running with `WhatIfONLY: true` shows changes you didn't expect -- tables being modified, columns being added or dropped.

**Cause:** The live database has drifted from what the schema package defines. WhatIf is showing you the delta between your package and the actual database state. This is WhatIf doing exactly what it should.

**Fix:**

1. Compare your schema package against the live database to identify what drifted.
2. If someone made manual changes to the database, decide whether to update your package (cast with SchemaTongs, as described in [Defining Your Schema -- Extracting Changes](04-defining-your-schema.md#extracting-changes-from-a-live-database)) or let SchemaQuench bring the database back in line.
3. If your package has unexpected definitions, check for uncommitted changes or the wrong package version.

### ShouldApplyExpression not skipping a component

**Symptom:** You added `ShouldApplyExpression` to an index, column, or constraint, expecting it to be skipped on the current database, but SchemaQuench deployed it anyway.

**Cause:** The expression returned something truthy. An empty result, `NULL`, or missing value is treated as "apply normally" rather than "skip" -- only an explicit `0`, `false`, or falsy scalar tells SchemaQuench to skip the component.

**Fix:**

1. Run the expression directly against the target database and check its return value.
2. If you're using custom-property tokens from `Extensions`, verify the token actually resolves. Unresolved `{{Table.SomeName}}` tokens are left in place literally and usually cause the SQL to error out rather than return a clean `0` or `1`.
3. Wrap the expression in `SELECT CASE WHEN ... THEN 1 ELSE 0 END` to make the output shape unambiguous.

### Custom property tokens show up literally in generated SQL

**Symptom:** The generated SQL contains literal `{{Table.MyProperty}}` tokens instead of the resolved value.

**Cause:** The expected custom property isn't inside the `Extensions` object on the correct component. Perhaps it's flat on the object instead of wrapped in `Extensions`, or it's on the wrong component (table vs column), or the name is misspelled.

**Fix:**

1. Verify the custom property is inside an `Extensions` object on the intended component. Custom values are **not** flat on the class -- they must live inside `Extensions`. See [Custom Properties](../reference/custom-properties.md) for the shape.
2. Check the scope: bare `{{PropertyName}}` reads from the component's own Extensions; `{{Table.PropertyName}}` reads from the parent table's Extensions.
3. Remember token names are case-insensitive but the path components (`Table.`, nested object keys) must match exactly.

### Secondary server scripts running on the wrong replica

**Symptom:** You configured `SecondaryServers` on the target but scripts are only running on the primary, or scripts meant for the primary are running on secondaries.

**Cause:** The `ServerToQuench` setting on your product-level folders isn't set correctly, or you're using a template-level folder (which always runs against the identified database, not across replicas).

**Fix:** Only `Product`-level folders (declared via `Product.Folders`) participate in secondary-server routing. On each folder, set `ServerToQuench` to `Primary`, `Secondary`, or `Both` explicitly. See [Schema Packages -- Secondary Servers](../reference/schema-packages.md#secondary-servers) for the full pattern.

---

## Common extraction issues (SchemaTongs)

### Encrypted objects warning (SQL Server)

**Symptom:** The progress log shows `WARNING: [schema].[object] is encrypted, skipping`.

**Cause:** SQL Server objects created with `WITH ENCRYPTION` can't have their source code retrieved. That's a platform limitation, not a SchemaTongs issue.

**Fix:** No action needed if you expect these objects to be encrypted. If you need to manage them through SchemaSmith, they must be recreated without encryption.

### Objects not appearing in extraction output

**Symptom:** You know an object exists in the database, but SchemaTongs didn't cast it.

**Cause:** SchemaTongs filters extraction based on two settings:

1. **ShouldCast flags** -- Each object type has a flag (`ShouldCast:Views`, `ShouldCast:Procedures`, `ShouldCast:MaterializedViews`, etc.) that defaults to `true`. If set to `false`, that entire category is skipped.
2. **ObjectList filter** -- If `ShouldCast:ObjectList` is set, only the explicitly listed objects are extracted.

**Fix:** Check your settings file:

- Verify the `ShouldCast` flag for the object type isn't set to `false`.
- If you're using `ObjectList`, make sure the object is included in the comma-separated list.
- Remember that per-platform flags are ignored on other platforms. A PostgreSQL-only flag like `Sequences` won't affect a SQL Server extraction.

See [SchemaTongs Reference](../reference/schematongs.md) for the full per-platform flag breakdown.

### Custom properties disappearing on re-extraction

**Symptom:** You added custom data under `Extensions` on a table. After re-extracting with SchemaTongs, the `Extensions` content is gone.

**Cause:** Preservation matches by the component's `Name`. If you renamed a column or a table without setting `OldName`, SchemaTongs sees the new component as brand-new and writes a fresh file without the old Extensions content.

**Fix:**

1. Before renaming, set `OldName` on the component. SchemaTongs uses `OldName` as a fallback when matching components for preservation.
2. If the Extensions data is on a platform-specific component that isn't in the preservation table (check [Custom Properties -- Preservation During Re-extraction](../reference/custom-properties.md#preservation-during-re-extraction)), you'll need to re-add the data manually after the extraction.

### Orphan warnings

**Symptom:** The progress log shows "orphaned file(s)" detected in one or more folders.

**Cause:** SchemaTongs found files in the template directory that don't correspond to any object in the live database. This usually means the object was dropped or renamed in the database since the last extraction.

**Fix:** Review the listed files. Set `OrphanHandling:Mode` to `DetectWithCleanupScripts` to generate DROP scripts, or `DetectDeleteAndCleanup` to also delete the orphan files. Orphan detection is skipped when `ObjectList` is active, since a partial extraction can't determine what's truly orphaned.

### Script validation errors (.sqlerror files)

**Symptom:** Some extracted files have a `.sqlerror` extension instead of `.sql`.

**Cause:** When `ShouldCast:ValidateScripts` is enabled, SchemaTongs checks each extracted script for validity. Scripts that fail validation are saved with the `.sqlerror` extension.

**Fix:**

1. Open the `.sqlerror` file to see the raw extracted content and understand what went wrong.
2. Common causes: the object depends on other objects that don't exist in the validation context, or the object uses syntax that the parser can't validate in isolation.
3. If the scripts are actually valid (false positives from isolated validation), you can set `ShouldCast:SaveInvalidScripts` to `false` to discard them, or disable validation with `ShouldCast:ValidateScripts: false`.

---

## Common DataTongs issues

### "Could not determine key columns"

**Symptom:** The progress log shows a message like "Table [name] has no primary key or unique index and no KeyColumns configured. Skipping table."

**Cause:** DataTongs generates sync scripts that need a key to match source and target rows. It looks for a primary key first, then a unique index. If neither exists, it can't proceed.

**Fix:** Specify key columns manually in your DataTongs configuration:

```json
{
  "Tables": [
    { "Name": "dbo.MyTable", "KeyColumns": "[Column1],[Column2]" }
  ]
}
```

If a key column is nullable, prefix it with `*` to generate NULL-safe comparisons: `"KeyColumns": "*[NullableCol],[NonNullableCol]"`.

### Column types excluded from output

**Symptom:** Certain columns are missing from the generated sync script.

**Cause:** DataTongs automatically excludes columns whose types can't be reliably round-tripped through JSON. The list is platform-specific -- see [Edge Cases -- Complex type handling in DataTongs](10-edge-cases.md#complex-type-handling-in-datatongs).

**Fix:** Expected behavior. If you need data from these columns, handle them with custom scripts outside of DataTongs.

### Empty output (no script generated)

**Symptom:** DataTongs runs without errors but says "No data found -- skipping script generation" for a table.

**Cause:** The table is empty in the source database, or your `Filter` expression excludes all rows.

**Fix:**

- Verify the table has data in the source database.
- If you specified a `Filter`, run the equivalent `WHERE` clause against the source to confirm it matches rows.
- Check that you're connecting to the correct source database (`Source:Database` in settings).

### Table does not exist in source database

**Symptom:** The progress log shows "Table [schema].[name] does not exist in source database. Skipping table."

**Cause:** The table name in your configuration doesn't match any table in the source database.

**Fix:** Check the table name for typos. DataTongs expects the format `schema.tablename`. If the schema is omitted, the platform default is assumed (`dbo` on SQL Server, `public` on PostgreSQL, the connection database on MySQL).

---

## Environment and platform issues

### Cross-platform path issues

**Symptom:** Paths work on one operating system but fail on another.

**Fix:** Use forward slashes (`/`) in configuration files. SchemaSmith normalizes paths internally, but forward slashes are valid on all platforms (Windows, macOS, Linux).

```json
{ "Product": { "Path": "./my-product" }, "ContentPath": "./my-product/data", "ScriptPath": "./my-product/Table Data" }
```

Avoid backslashes in JSON configuration -- they require escaping (`\\`) and reduce portability.

### Docker database server not responding

**Symptom:** Connection failures when targeting a database server running in Docker.

**Fix:**

1. Verify the container is running: `docker ps`
2. Check the port mapping -- if you mapped the container's default port to a non-default host port, specify it in `Target:Port`.
3. Confirm the password meets the engine's complexity requirements (SQL Server's SA password rules are strict).
4. On SQL Server, add `TrustServerCertificate: "True"` to your connection properties since Docker containers typically use self-signed certificates.
5. On PostgreSQL, check the `pg_hba.conf` in the container allows connections from your host (the official image is permissive by default).
6. On MySQL, the official image may require `AllowPublicKeyRetrieval=True` in `ConnectionProperties` for first-run SSL key retrieval.

### Environment variables not taking effect

**Symptom:** You set an environment variable but the tool doesn't use the value.

**Cause:** SchemaSmith environment variables require a specific prefix and separator format.

**Fix:**

- Prefix all variables with `SmithySettings_`.
- Use double underscores (`__`) to represent hierarchy levels.
- Restart your shell after setting variables (or use `export` in the current session).

```bash
export SmithySettings_Target__Server=my-server
export SmithySettings_Target__User=deploy_user
export SmithySettings_Target__Password=secret
```

These map to `Target:Server`, `Target:User`, and `Target:Password` in the configuration hierarchy. See [Configuration Reference](../reference/configuration.md) for the full precedence rules.

### Platform mismatch errors

**Symptom:** SchemaQuench reports an error about unknown object types, SQL syntax failures that look like they're from a different database engine, or odd behavior when an extraction doesn't match deployment.

**Cause:** The `Platform` value in `Product.json` doesn't match the target server. If your product was extracted from PostgreSQL but you point SchemaQuench at a SQL Server target, the DDL adapter mismatch will produce very strange errors.

**Fix:** Verify `Product.json` has the right `Platform` value (`SqlServer`, `PostgreSQL`, or `MySQL`) and that your target connection points to a matching server. One repository can host products targeting different platforms -- just never mix them at deployment time.

---

## Still stuck?

If your issue isn't covered here, check the [reference documentation](../README.md#reference) for detailed behavior descriptions, or open an issue on [GitHub](https://github.com/Schema-Smith/SchemaSmithyFree/issues).

If you're still stuck and want to talk it through, reach out to Forge directly -- [ForgeBarrett@SchemaSmith.com](mailto:ForgeBarrett@SchemaSmith.com). Real developers on the other end. Real answers. We're happy to help.

---

This is the final chapter of the guide. For a refresher on the basics, head back to [Why SchemaSmith](01-why-schemasmith.md) or jump straight to the [Quick Start](02-quick-start.md).
