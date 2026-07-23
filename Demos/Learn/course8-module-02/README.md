# Course 8 · Module 2 — Structure-change failures

Two induced incidents on one database (`diag_structure`), read end to end. This is the **structure
half** of the mechanical engine: how column *adds* (`MissingTableAndColumnQuench`) and column
*alters* (`ModifiedTableQuench`) fail, and how to read them. Same method as Module 1 — locate the
phase, read the error, recover.

## Prerequisites

- The four-engine sandbox is up (`Demos/Learn/docker`) — see [`../README.md`](../README.md).
- `schemaquench --version` answers on your PATH. New to the CLI? [Course 1, Module 1](https://learn.schemasmith.com/01-install-connect/).

## Step 1 — create the sandbox database

**macOS / Linux:** `cd Demos/Learn/course8-module-02 && bash setup-databases.sh`
**Windows:** `cd Demos\Learn\course8-module-02 ; .\setup-databases.ps1`

Prints `PASS` per engine once `diag_structure` exists. Re-running is safe (guarded `CREATE`).

## Step 2 — deploy the baseline (green)

```
cd sqlserver            # or postgres, mysql, or mariadb
schemaquench --ConfigFile:quench.settings.baseline.json --LogPath:"$PWD/logs"
```

Exits `0` and forges the `Shop` schema into `diag_structure`, then a run-once seed populates
`Customer` with four rows — one of them `FullName = 'Ana Fielding-Reyes'` (18 chars). A **populated**
table is the precondition both incidents need.

## Beat 1 — adding a required column over existing data (`4901` / `23502`)

Deploy the change that adds a `NOT NULL` column with no default:

```
schemaquench --ConfigFile:quench.settings.beat1-broken.json --LogPath:"$PWD/logs"
```

**Read the trail — and notice the engines disagree.** Same package, three outcomes:

| Engine | Result |
| --- | --- |
| **SQL Server** | **Fails, exit `2`** at `Quenching missing tables and columns`: *"ALTER TABLE only allows columns to be added that can contain nulls, or have a DEFAULT … Column 'LoyaltyTier' cannot be added to non-empty table 'Customer'…"* (error `4901`). |
| **PostgreSQL** | **Fails, exit `2`** at the same phase: `23502: column "loyaltytier" of relation "customer" contains null values`. |
| **MySQL** | **Exits `0`.** No failure — MySQL adds the column and **silently backfills `''`** into every existing row. Even in `STRICT_TRANS_TABLES` mode. |
| **MariaDB** | **Exits `0`.** Identical to MySQL — it adds the column and **silently backfills `''`** into every existing row, even in `STRICT_TRANS_TABLES` mode. |

That MySQL/MariaDB row is the lesson: **the engine that fails loud is protecting you.** SQL Server and
PostgreSQL refuse a required column with no value for the rows already there. MySQL and MariaDB just fill
blanks — you get a `LoyaltyTier` column full of empty strings and no warning. (This is engine
behavior, not SchemaSmith — SchemaSmith issues the same correct `ALTER` everywhere; the two MySQL-family
engines choose to fill rather than refuse.)

**The fix** is what SQL Server / PostgreSQL were asking for: give the new column a `Default`, so the
rows already in the table get a real value. `beat1-fixed/` does exactly that (`Default 'Standard'`):

```
schemaquench --ConfigFile:quench.settings.beat1-fixed.json --LogPath:"$PWD/logs"
```

Green on all four. On SQL Server / PostgreSQL the existing rows now read `Standard`. **On MySQL and
MariaDB they stay `''`** — the column was already added back in `beat1-broken`, and a default only
applies to *new* rows. If MySQL and MariaDB had failed loud like the others, you'd have caught it
before the blanks landed.

## Beat 2 — narrowing a column that still holds long data (`8152` / `22001` / `1406`)

Now a column *alter*. `beat2-broken/` narrows `FullName` from `NVARCHAR(200)` to `NVARCHAR(10)`, but
`'Ana Fielding-Reyes'` is 18 characters:

```
schemaquench --ConfigFile:quench.settings.beat2-broken.json --LogPath:"$PWD/logs"
```

This one fails the **same way on all four** — exit `2` at `Quenching modified tables`:

| Engine | Error |
| --- | --- |
| **SQL Server** | `String or binary data would be truncated in table 'diag_structure.dbo.Customer', column 'FullName'.` (error `8152`) |
| **PostgreSQL** | `22001: value too long for type character varying(10)` |
| **MySQL** | `Data too long for column 'FullName' at row 1` (error `1406`) |
| **MariaDB** | `Data too long for column 'FullName' at row 1` (error `1406`) |

**The fix is data, not schema** — the existing values are too long for the shape you asked for.
Shorten them, then redeploy the same change:

```
-- SQL Server (PG / MySQL / MariaDB analogous)
UPDATE dbo.Customer SET FullName = LEFT(FullName, 10);
```
```
schemaquench --ConfigFile:quench.settings.beat2-broken.json --LogPath:"$PWD/logs"
```

Green on all four — the narrow now applies. (This redeploy runs on the checkpointed
`ModifiedTables` phase with the failed run's checkpoint still present; PostgreSQL re-converges
cleanly here.)

## What each folder is

| Path | Purpose |
| --- | --- |
| `baseline/` | Healthy `Shop` + the run-once seed that populates `Customer`. |
| `beat1-broken/` | Baseline + a `NOT NULL LoyaltyTier` column, **no default** — the `4901`/`23502` incident. |
| `beat1-fixed/` | `beat1-broken` + `Default 'Standard'` — the recovery. |
| `beat2-broken/` | `beat1-fixed` + `FullName` narrowed to 10 — the `8152`/`22001`/`1406` incident. |
| `quench.settings.<state>.json` | One per package, all targeting `diag_structure`, lab-local `artifacts`/`checkpoints`. |

Next: **Module 3 — Index, constraint & FK failures**, where the dup-key incident from Module 1 gets
its full diagnosis. The recovery *toolkit* (`--ResumeQuench`, marking a script done) is **Module 5**.
