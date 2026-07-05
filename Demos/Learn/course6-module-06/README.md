<!-- TRAINING-RELEASE-PIN #324: --Validate semantic linter (SchemaSmith#324, merged in #326). When --Validate ships in a stock release: bump the pre-flight version string, drop the from-source caveat, simplify ci/validate.yml to the plain install step, re-cert, then delete this sentinel + the release-coupled table row in training-roadmap.md. -->

# Course 6, Module 6 — Lint before you deploy: the `--Validate` semantic linter (lab)

**Goal:** catch the cross-object errors JSON Schema can't express — a dangling foreign key, an ungated duplicate column, an undefined token, a stale editor schema — with a single static command that needs no database, no credentials, and no running engine. Read a broken package's findings board, fix each error to green, prove the linter is *semantic* (not dumb), clear an induced staleness finding, then wire it in as a CI gate. SQL Server, PostgreSQL, and MySQL.

## The scenario

Structural validation (Module 3) catches the typo — a missing `DataType`, a misspelled property — on every pull request with no database. But it validates each file in isolation. It can't see that a foreign key points at a table you forgot to include, that two columns collide on a name, or that a `ShouldApplyExpression` references a token nobody defined. Those are errors of *meaning across objects*, and they only surface at deploy time — the most expensive place to find them.

`--Validate` closes that gap. It's a static, target-less semantic linter: it loads the whole package, checks cross-object meaning, prints a findings board, and exits `2` if anything is wrong. No database. No connection string. Seconds to run. It belongs first in your pre-flight family, ahead of every gate that needs a live engine.

## Before you start

- **No sandbox, no database, no credentials.** `--Validate` (and `schematongs --WriteSchemasOnly`, used later) never connect to an engine. This lab is entirely database-free.
- **From-source override.** `--Validate` merged to `main` (SchemaSmith [#324](https://github.com/Schema-Smith/SchemaSmith/issues/324), PR [#326](https://github.com/Schema-Smith/SchemaSmith/pull/326)) but isn't in the 2.2.0 release yet. Build the CLI from source, then set `SCHEMAQUENCH` / `SCHEMATONGS` to the built executables and use them in the commands below:
  ```
  export SCHEMAQUENCH="/path/to/SchemaSmith/SchemaQuench/bin/Release/net10.0/SchemaQuench.exe"
  export SCHEMATONGS="/path/to/SchemaSmith/SchemaTongs/bin/Release/net10.0/SchemaTongs.exe"
  ```
  Once `--Validate` ships in a stock release, drop this step and use the installed `schemaquench` / `schematongs` on your PATH.
- Each engine package under `sqlserver/`, `postgres/`, and `mysql/` ships **deliberately broken** — that's the starting point. You'll fix it.

> **Path binding note:** if `--SchemaPackagePath:./sqlserver/Package` doesn't bind in your shell, pass the absolute path via the environment instead: `SmithySettings_SchemaPackagePath="$(pwd)/sqlserver/Package" "$SCHEMAQUENCH" --Validate`. Run `schematongs --WriteSchemasOnly` from *inside* a `Package` directory (it defaults to `.`).

## Scenario 1 — read the board

Run the linter against the broken SQL Server package (swap `sqlserver` for `postgres` / `mysql` — the same three errors reproduce on every engine):

```
"$SCHEMAQUENCH" --Validate --SchemaPackagePath:./sqlserver/Package
```

It exits `2` and prints exactly three errors, one from each check engine:

```
ERROR [SS-DUP-001] Template 'Main' / Table '[OrderItem]': Duplicate column name '[Quantity]' at Template 'Main' / Table '[OrderItem]' - 2 entries share this name and at least one is not gated by ShouldApplyExpression.
ERROR [SS-FK-002] Template 'Main' / Table '[OrderItem]' / FK '[FK_OrderItem_Supplier]': RelatedTable '[Supplier]' does not resolve to any known table (resolved schema '[dbo]').
ERROR [SS-TOK-001] .../dbo.Customer.json: references undefined token '{{IncludePiiColumns}}'.
3 error(s), 0 warning(s)
```

Three real errors, no database touched:

- **`SS-DUP-001`** — `OrderItem` has two `[Quantity]` columns and neither is gated. A duplicate that would blow up at `CREATE TABLE`.
- **`SS-FK-002`** — `OrderItem` declares `[FK_OrderItem_Supplier]` pointing at a `[Supplier]` table that isn't in the package. You forgot to include the table.
- **`SS-TOK-001`** — `Customer`'s `[Email]` column is gated on `{{IncludePiiColumns}}`, a token nobody defined. It would silently evaluate to nothing at deploy.

## Scenario 2 — clear the board

Fix each error and re-run. Three edits:

1. **`SS-DUP-001`** — in `sqlserver/Package/Templates/Main/Tables/dbo.OrderItem.json`, remove the duplicate ungated `[Quantity]` column (keep the original).
2. **`SS-FK-002`** — in the same file, remove the `[SupplierId]` column *and* the `[FK_OrderItem_Supplier]` foreign key (the `[Supplier]` table was never part of this package).
3. **`SS-TOK-001`** — in `sqlserver/Package/Product.json`, add `"IncludePiiColumns": "1"` to `ScriptTokens` to declare the token the `[Email]` column references.

Re-run:

```
"$SCHEMAQUENCH" --Validate --SchemaPackagePath:./sqlserver/Package
```

```
PASS - no issues found
```

Exit `0`. Green board.

## Scenario 3 — smart, not dumb

Before you fixed anything, `Product` already had *two* columns named `[Discontinued]` — and the linter never flagged them. That's the point.

```json
{ "Name": "[Discontinued]", "DataType": "BIT",     "ShouldApplyExpression": "'{{Edition}}' = 'Legacy'", "VariantName": "Legacy" },
{ "Name": "[Discontinued]", "DataType": "TINYINT", "ShouldApplyExpression": "'{{Edition}}' = 'Modern'", "VariantName": "Modern" }
```

Both are gated on the **defined** `{{Edition}}` token, and each carries a distinct `VariantName`. That's a legitimate variant pair — the same logical column, materialized differently per edition, only ever one at a time. `--Validate` understands the difference between a variant pair and a collision. The two ungated `[Quantity]` columns were a bug; the two gated `[Discontinued]` columns are a feature. A dumb name-uniqueness check would flag both. A semantic linter flags only the first.

## Scenario 4 — when your schemas drift

The editor `.json-schemas` that give you red-squiggle validation in your IDE are generated from the domain model. If the model changes and nobody regenerates them, they lie. `--Validate` catches that too. Induce it:

1. Hand-edit `sqlserver/Package/.json-schemas/tables.sqlserver.schema` — narrow any `"maxLength": 128` to `"maxLength": 1`, so it no longer matches fresh generation.
2. Re-run `--Validate`:
   ```
   ERROR [SS-STALE-001] .../tables.sqlserver.schema: committed .json-schemas are stale - regenerate via --WriteSchemasOnly.
   ```
   Exit `2`.
3. Regenerate (database-free), from inside the `Package` directory:
   ```
   cd sqlserver/Package && "$SCHEMATONGS" --WriteSchemasOnly && cd ../..
   ```
4. Re-run `--Validate` → `PASS - no issues found`, exit `0`. The staleness finding is gone.

## Scenario 5 — make it a gate

`ci/validate.yml` is a copy-ready GitHub Actions workflow. Copy it into your repository's `.github/workflows/` and adjust the package path. It runs `--Validate` on every pull request; the exit-2-on-error behavior fails the PR automatically — no database, no credentials, no matrix of engine containers. Because it needs no live engine, it's the cheapest gate you have: run it first, ahead of anything that connects.

## Cross-platform

The same three-error board reproduces on all three engines. Only the identifier quoting and native type spellings differ (`[dbo]` schema and bracket quoting on SQL Server; lowercase `public` and unquoted lowercase identifiers on PostgreSQL; backtick-quoted, schema-less names on MySQL). On MySQL, foreign-key resolution is **name-only** — there are no schemas within a database — so `SS-FK-002` resolves `Supplier` by bare name. The finding codes, the switch names, the exit behavior, and the pass/fail semantics are identical everywhere.
