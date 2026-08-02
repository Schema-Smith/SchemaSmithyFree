# SQL Server Compatibility-Level Gate Demo

One schema package. One server. Two databases. Different deployed code — because on SQL Server, the *server* version is not what decides which syntax you can use.

## The trap this demo exists for

On PostgreSQL, MySQL and MariaDB, "what version is this server?" answers "what syntax can I use?". **On SQL Server it does not.** A modern binary can host a database left at an old *compatibility level*, and a good deal of newer syntax fails there — at parse time — even though the server is current.

So the gate everyone reaches for first is wrong:

```sql
-- WRONG for syntax gating. Returns 16 on a 2022 server regardless of the
-- database's compatibility level, so it happily green-lights syntax that
-- will not parse in a compat-130 database on that same server.
SERVERPROPERTY('ProductMajorVersion') >= 16
```

```sql
-- RIGHT. Asks the question that actually governs syntax availability.
(SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) >= 160
```

Rule of thumb: **gate syntax on compatibility level, gate server features on server version.** They are different questions, and only SQL Server makes you ask both.

## What's in here

One SQL Server 2022 instance hosting two databases:

| Database | Compatibility level | Gets |
|---|---|---|
| `AppDb_Modern` | 160 | the `GENERATE_SERIES` view |
| `AppDb_Legacy` | 130 (SchemaSmith's floor) | the recursive-CTE view |

A single table (`dbo.Reading`) and **two variants of the same view** (`dbo.vReadingCalendar`), each in its own folder, each gated in [`Template.json`](Package/Templates/Main/Template.json):

```json
"ScriptFolders": [
  {
    "FolderPath": "Programmability/Modern",
    "QuenchSlot": "Objects",
    "ObjectType": "Views",
    "ShouldApplyExpression": "(SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) >= 160"
  },
  {
    "FolderPath": "Programmability/Legacy",
    "QuenchSlot": "Objects",
    "ObjectType": "Views",
    "ShouldApplyExpression": "(SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) < 160"
  }
]
```

Both variants produce the same view name, the same columns and the same 30 rows. The modern one builds its day series with `GENERATE_SERIES` (SQL Server 2022, and **compatibility-level 160 gated**); the legacy one uses a recursive CTE that works at any supported level.

This is **folder-level gating** — the coarsest of the three levers, and the right one when the thing that differs is an entire object's implementation rather than one column.

## Running it

```bash
./run-demo.sh        # macOS / Linux
run-demo.cmd         # Windows
```

Actual output:

```
--- The server version is IDENTICAL for both databases ---
DatabaseName ServerMajor CompatLevel
------------ ----------- -----------
AppDb_Legacy 16 130
AppDb_Modern 16 160

--- AppDb_Legacy (compat 130): recursive-CTE variant deployed ---
VariantDeployed
---------------
recursive CTE (any compat)
CalendarRows
------------
30

--- AppDb_Modern (compat 160): GENERATE_SERIES variant deployed ---
VariantDeployed
---------------
GENERATE_SERIES (needs compat 160)
CalendarRows
------------
30

--- Negative control: what the modern view would have hit in AppDb_Legacy ---
Msg 208, Level 16, State 1, Server 1282741cd773, Line 1
Invalid object name 'GENERATE_SERIES'.
```

Read the first block and the last block together. **`ServerMajor` is 16 for both databases** — and `GENERATE_SERIES` still fails in one of them. That is the whole argument for gating on compatibility level: a server-version gate cannot tell these two apart, and would have deployed a view that does not parse.

## Why one container is the point

The sister version-gate demos run two servers because the difference they demonstrate *is* a server difference. This one deliberately runs **one** server, because the difference it demonstrates is not. If you ever find yourself reaching for a second container to show this, the demo has lost its meaning.

`MSSQL_IMAGE` in [`.env`](.env) is overridable, but it must stay at SQL Server 2022 (16.x) or later — the modern variant needs a binary that has `GENERATE_SERIES` at all.

## Why this matters

Compatibility level is sticky. Databases restored from older servers, migrated from an EOL instance, or deliberately pinned to protect a query plan all sit below their host's level — often for years, and usually without anyone tracking which ones. A fleet on entirely current binaries can still be a mixed-syntax fleet.

`ShouldApplyExpression` lets the schema package ask the question that actually matters, per database, at deploy time — instead of encoding an assumption about the fleet in a CI script that no one revisits.

## Cleanup

```bash
docker compose down --volumes
```

## Related

- Sister demos:
  - [`../SqlServer-RollingRollout`](../SqlServer-RollingRollout) — same engine, **state**-based gating (a DBA approval row) instead of version-based
  - [`../PostgreSQL-VersionGate`](../PostgreSQL-VersionGate) — server-version gate on PG18+
  - [`../MySQL-VersionGate`](../MySQL-VersionGate) — server-version gate on MySQL 9+ (major-only)
  - [`../MariaDB-VersionGate`](../MariaDB-VersionGate) — server-version gate on MariaDB 10.7+ (major **and** minor)
