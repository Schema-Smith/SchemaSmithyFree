# Course 4, Recipe 2 — Policy that enforces itself (lab)

Goal: declare a retention policy **once**, as nested custom-property metadata on the table, and have it
drive two things at deploy time — a column's `Default` and a check constraint that enforces the bound.
Change the policy value, re-quench, and the column **default follows** the new number. The number lives
in one place; the schema carries it.

Each engine folder (`sqlserver/`, `postgres/`, `mysql/`) ships the full `Package/` plus `deploy.settings.json`,
all targeting `cookbook_r2`.

## Before you start

- The [sandbox](../docker) is up and the Course 4 databases exist (run [`../course4-setup`](../course4-setup) once — it creates `cookbook_r2`).
- The CLI is on your PATH (`schemaquench --version` → `2.1.0.0` or later).

## Step 1: Look at the policy and what it drives

Open `Tables/…Document.json`. The table carries a nested custom property:

```json
"Extensions": { "Retention": { "ArchiveDays": "90" } }
```

Two objects read it back through the `{{Table.Retention.ArchiveDays}}` token (nested values flatten with dots):

- the `ArchiveAfterDays` column: `"Default": "{{Table.Retention.ArchiveDays}}"` → a default of `90`;
- a check constraint in the `CheckConstraints` array:
  `"Expression": "[RetentionDays] <= {{Table.Retention.ArchiveDays}}"` → enforces `<= 90`.

One declared number, two places it lands — and you never typed `90` into either the default or the check.

## Step 2: Deploy and watch the policy take hold

```bash
cd <engine>
schemaquench --ConfigFile:deploy.settings.json
```

The default and the check are both built from the metadata:

```bash
# SQL Server
docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d cookbook_r2 -Q \"SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.Document')\""
# → ([RetentionDays]<=(90))
```

Prove the default applies and the check enforces:

```sql
-- omit ArchiveAfterDays → it defaults to 90
INSERT INTO Document (DocumentId, Title, RetentionDays) VALUES (1, 'Contract', 50);   -- OK
-- exceed the policy → the check rejects it
INSERT INTO Document (DocumentId, Title, ArchiveAfterDays, RetentionDays) VALUES (2, 'TooLong', 90, 120);
-- → CHECK constraint violation (Msg 547 / "violates check constraint" / Error 3819)
```

## Step 3: Change the policy once — the default follows

Edit the metadata — `ArchiveDays` from `90` to `30` — and re-quench (clear any rows that would violate the
tighter bound first):

```bash
schemaquench --ConfigFile:deploy.settings.json
```

The column default re-derives from the new number, on all three engines:

```bash
# PostgreSQL
docker exec learn-postgres psql -U postgres -d cookbook_r2 -tAc "SELECT column_default FROM information_schema.columns WHERE table_name='document' AND column_name='archiveafterdays'"
# → 30
```

You didn't touch the column. You changed the *policy on the table*, and the next ordinary quench rebuilt the
default from it. The number you declared once is the only place the value lives.

## Per-engine notes

| | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| Nested token | `{{Table.Retention.ArchiveDays}}` | same | same |
| Default from metadata | `DEFAULT ((90))` | `DEFAULT 90` | `DEFAULT 90` |
| Check from metadata | `([RetentionDays]<=(90))` | `CHECK ((retentiondays <= 90))` | `` (`RetentionDays` <= 90) `` |
| Default follows a policy change | ✅ | ✅ | ✅ |

The `Default` and the check are both authored once on the table and resolve identically across the three
engines; only the dialect's constraint syntax differs.

## The principle

The retention number used to live in three places — the column default, the constraint, and a wiki page
nobody updated. Here it lives in exactly one: a custom property on the table. The default reads it, the check
reads it, and when the policy changes you change the *policy*, not the columns. Declare the rule once on the
object; let the schema carry it.
