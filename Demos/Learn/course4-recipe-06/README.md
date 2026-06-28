# Course 4, Recipe 6 — Surviving a destructive rebuild (lab)

Goal: keep a table's **data** when a change removes it from the product (or forces a drop-and-recreate),
using the two recyclebin hooks SchemaQuench looks for. You install `CustomTableDrop` and
`CustomTableRestore` procedures once; from then on the engine routes a removed table's drop through your
drop hook (instead of a hard `DROP`), and calls your restore hook when the table comes back — and won't
recreate it empty if the hook already did. These hooks "soft-drop" by renaming the table aside, so its
structure **and** data ride through the rebuild.

Each engine folder (`sqlserver/`, `postgres/`, `mysql/`) ships `install-recyclebin.sql`, a `Package/` (with a
`Promotion` table) and a `Package-NoPromotion/` (the same product *without* `Promotion`), plus
`deploy.settings.json` and `remove-promotion.settings.json`, all targeting `cookbook_r6`.

## Before you start

- The [sandbox](../docker) is up and the Course 4 databases exist (run [`../course4-setup`](../course4-setup) once — it creates `cookbook_r6`).
- The CLI is on your PATH (`schemaquench --version` → `2.1.0.0` or later).

## Step 1: Deploy, then install the hooks

```bash
cd <engine>
schemaquench --ConfigFile:deploy.settings.json     # creates Keeper + Promotion, and kindles SchemaSmith
```

Then install the recyclebin hooks (kindling created the `SchemaSmith` schema they live in) — run the one for your engine:

```bash
# SQL Server
docker exec -i learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d cookbook_r6" < install-recyclebin.sql

# PostgreSQL
docker exec -i learn-postgres psql -U postgres -d cookbook_r6 < install-recyclebin.sql

# MySQL
docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd cookbook_r6 < install-recyclebin.sql
```

## Step 2: Put data in the table

```sql
INSERT INTO Promotion (PromotionId, Code, DiscountPct) VALUES (1, 'SAVE10', 10), (2, 'SAVE20', 20);
```

## Step 3: Remove the table from the product — the drop hook archives it

Deploy the package that no longer defines `Promotion`. Normally SchemaQuench would drop it (and the rows go
with it). With the hook installed, the drop is routed through `CustomTableDrop`, which renames the table aside:

```bash
schemaquench --ConfigFile:remove-promotion.settings.json
```

```bash
# the table is gone under its own name, but its rows are safe in the aside copy
docker exec learn-postgres psql -U postgres -d cookbook_r6 -tAc "SELECT count(*) FROM public.__recyclebin__promotion"
# → 2
```

## Step 4: Bring it back — the restore hook returns the data

Deploy the package that defines `Promotion` again. `CustomTableRestore` fires first, renames the aside copy
back, and the engine — seeing the table now exists — does **not** recreate it empty:

```bash
schemaquench --ConfigFile:deploy.settings.json
docker exec learn-postgres psql -U postgres -d cookbook_r6 -tAc "SELECT promotionid, code FROM public.promotion ORDER BY promotionid"
# → 1 SAVE10
#   2 SAVE20
```

The original rows survived the round-trip. The data was never dropped — it was set aside and brought back.

## Per-engine notes

| | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| Hook procedures | `SchemaSmith.CustomTableDrop` / `…Restore` | `"SchemaSmith"."CustomTableDrop"` / `…Restore` | `SchemaSmith_CustomTableDrop` / `…Restore` (in the target DB) |
| Parameters | `@SchemaName, @TableName` | `p_Schema, p_Table` | `p_DatabaseName, p_TableName` |
| Soft-drop mechanism | `sp_rename` | `ALTER TABLE … RENAME TO` | `ALTER TABLE … RENAME TO` |

SchemaQuench detects the hooks by procedure existence, per database. The names and parameter conventions
differ per engine; the behavior — route the drop, restore before recreate — is identical.

## The principle

A destructive schema change shouldn't mean a held breath and a manual backup. The recyclebin hooks make
drop-and-recreate **reversible**: your drop hook decides what "drop" means (here, set the table aside intact),
and your restore hook brings it back before the engine would recreate it empty. Install them once, and every
removal becomes a soft-drop you can undo — a safety net under the most dangerous kind of deploy.
