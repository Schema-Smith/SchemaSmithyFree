# Course 4, Recipe 5 — Scripts that write scripts (lab)

Goal: a deploy-time script **reads your declared model** (the table's JSON) and **generates DDL from it** —
here, a `ProductSnapshot` table that mirrors `Product`'s columns, plus a copy of the current rows. Add a
column to `Product` and re-quench: the generated table grows to match and the next snapshot includes it. No
second declaration to keep in sync — the generated object is computed from the one source of truth.

This is different from cataloging your metadata (Course 2 reads the model into a table of *rows about* the
schema). Here the script **emits schema** — `CREATE TABLE`, `ALTER TABLE`, `INSERT` — built from the model.

Each engine folder (`sqlserver/`, `postgres/`, `mysql/`) ships the full `Package/` plus `deploy.settings.json`,
all targeting `cookbook_r5`.

## Before you start

- The [sandbox](../docker) is up and the Course 4 databases exist (run [`../course4-setup`](../course4-setup) once — it creates `cookbook_r5`).
- The CLI is on your PATH (`schemaquench --version` → `2.1.0.0` or later).

## Step 1: Look at the generator

In `Templates/Main/Template.json`, a token holds the `Product` table's JSON:

```json
"ScriptTokens": { "ProductTable": "<*SpecificTable*>dbo.Product" }
```

The `[ALWAYS]` after-script reads that JSON, walks the columns (name + type), and builds DDL: a `CREATE TABLE`
for the mirror the first time, an `ALTER TABLE ADD` for any newly-declared columns, and an `INSERT … SELECT`
that snapshots the current rows. It runs that DDL dynamically — so it always reflects the current model.

## Step 2: Deploy — the script generates the mirror

```bash
cd <engine>
schemaquench --ConfigFile:deploy.settings.json
```

`ProductSnapshot` now exists, with `Product`'s columns plus a `SnapshotAt`:

```bash
# SQL Server
docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d cookbook_r5 -Q \"SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ProductSnapshot') ORDER BY column_id\""
# → SnapshotAt, ProductId, Name, Price
```

## Step 3: Put rows in Product, re-quench — they're captured

```sql
INSERT INTO Product (ProductId, Name, Price) VALUES (1, 'Widget', 9.99), (2, 'Gadget', 19.99);
```

```bash
schemaquench --ConfigFile:deploy.settings.json
# ProductSnapshot now holds a snapshot of those rows
```

## Step 4: Add a column — the generated table follows

Add `Sku NVARCHAR(50)` (or `VARCHAR(50)`) to `Product`'s JSON and re-quench. `ProductSnapshot` gains the `Sku`
column, and the next snapshot includes it — you never touched `ProductSnapshot`'s definition, because it
doesn't have one. It's generated from `Product` every deploy.

```bash
# PostgreSQL
docker exec learn-postgres psql -U postgres -d cookbook_r5 -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='productsnapshot' ORDER BY ordinal_position"
# → snapshotat, productid, name, price, sku
```

## Per-engine notes

| | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| Read the model JSON | `OPENJSON` | `jsonb_array_elements` | `JSON_TABLE` |
| Run generated DDL | `EXEC(@sql)` | `EXECUTE` in a `DO` block | `PREPARE` / `EXECUTE` |
| Snapshot timestamp | `SYSUTCDATETIME()` | `clock_timestamp()` | `CURRENT_TIMESTAMP(6)` |

Same shape on all three — read the model as JSON, build `CREATE`/`ALTER`/`INSERT` from the column list, run
it. Only the dialect's JSON-shredding and dynamic-execution syntax differ.

## The principle

The generated table is computed from the declared model on every deploy, so it can never drift from it. You
don't maintain a parallel definition; you maintain `Product`, and the snapshot table is derived. That's the
move — when an object's shape is a function of your schema, generate it from the schema instead of hand-keeping
a copy. One source of truth, and the tools write the rest.
