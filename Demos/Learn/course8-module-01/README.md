<!-- TRAINING-RELEASE-PIN #332: PostgreSQL stale-checkpoint re-run (SchemaSmith#332, merged in PR #335). The dedupe re-green needs this — a stock pre-#335 CLI crashes `42P01: temp_existing_indexes` on the PostgreSQL re-run. When #332 ships in a stock release: drop the from-source note below, re-cert against stock, delete this sentinel + the release-coupled table row in training-roadmap.md. -->
# Course 8 · Module 1 — Reading the black box

One induced failure on one database (`diag_blackbox`), read end to end. Module 0 showed you the trail a *healthy* deploy leaves. This is the first deploy that stops on purpose — a unique index that won't take on dirty data — and the walk from `exit 2` to a named phase to green.

## Prerequisites

- The three-engine sandbox is up (`Demos/Learn/docker`) — see [`../README.md`](../README.md).
- `schemaquench --version` answers on your PATH. New to the CLI? [Course 1, Module 1](https://learn.schemasmith.com/01-install-connect/).
- **From-source override (PostgreSQL re-green only).** The PostgreSQL stale-checkpoint fix (SchemaSmith [#332](https://github.com/Schema-Smith/SchemaSmith/issues/332)) merged to `main` but isn't in a stock release yet. The dedupe re-green's *PostgreSQL* plain re-run needs it — on a stock pre-#332 CLI it crashes with `42P01: relation "temp_existing_indexes" does not exist`. Build the CLI from source for that step (the failure itself, and the SQL Server / MySQL re-green, run on any recent CLI). Once #332 ships in a stock release, use the installed CLI on your PATH.

## Step 1 — create the sandbox database

**macOS / Linux:** `cd Demos/Learn/course8-module-01 && bash setup-databases.sh`
**Windows:** `cd Demos\Learn\course8-module-01 ; .\setup-databases.ps1`

Prints `PASS` per engine once `diag_blackbox` exists. Re-running is safe (guarded `CREATE`).

## Step 2 — deploy the baseline (green)

```
cd sqlserver            # or postgres, or mysql
schemaquench --ConfigFile:quench.settings.baseline.json --LogPath:"$PWD/logs"
```

Exits `0` and forges the `Shop` schema into `diag_blackbox`, then a run-once seed populates `Customer` — including two rows that **share the email** `ana.f@shop.test` (CustomerId 1 and 7). Under the baseline's **non-unique** `IX_Customer_Email` that's perfectly legal. It's also the dirty data the next step trips on.

## Step 3 — deploy the change that fails

`after/` is the baseline with one edit: `IX_Customer_Email` flipped to **unique**. Deploy it over the duplicate emails:

```
schemaquench --ConfigFile:quench.settings.after.json --LogPath:"$PWD/logs"
```

**Exit `2`.** Now read the black box. Open `logs/SchemaQuench - Progress.log` and find the `FAILED to quench:` block — the error is right there, and it names the phase:

| Engine | `FAILED to quench:` block |
| --- | --- |
| **SQL Server** | `The CREATE UNIQUE INDEX statement terminated because a duplicate key was found for the object name 'dbo.Customer' and the index name 'IX_Customer_Email'. The duplicate key value is (ana.f@shop.test).` (error `1505`) |
| **PostgreSQL** | `23505: could not create unique index "ix_customer_email"` |
| **MySQL** | `Duplicate entry 'ana.f@shop.test' for key 'Customer.IX_Customer_Email'` (error `1062`) |

The line just above it points at the artifact: `Resolved SQL written to: ./artifacts/SchemaQuench - Quench Indexes ….sql`. That file is **copy-runnable** — a single proc call:

```sql
EXEC [diag_blackbox].SchemaSmith.MissingIndexesAndConstraintsQuench @ProductName = 'Shop', @WhatIf = 0
```

Paste it into your client and the phase fails identically, by hand — that's your reproduction. There's no `>>> FAILING BATCH` marker here; that's a *user-script* thing (Module 4). A mechanical phase like this one puts the error in `Progress.log` and hands you the one-line proc call. (On SQL Server the error also lands in `Errors.log`; on PostgreSQL and MySQL that file stays empty — `Progress.log` is the cross-engine place to read.)

**Name the phase:** `Quenching indexes and constraints`. That's what turns a wall of SQL into "the unique index couldn't be created because the data has duplicates." And notice `checkpoints/` — the failed run **left a checkpoint** behind (a green run deletes it; a failure keeps it).

## Step 4 — fix the data, redeploy

The index definition is right; the *data* isn't unique yet. Give the duplicate its own address:

```sql
-- SQL Server (PG / MySQL analogous)
UPDATE dbo.Customer SET Email = 'ana.f7@shop.test' WHERE CustomerId = 7;
```

Then redeploy the same change:

```
schemaquench --ConfigFile:quench.settings.after.json --LogPath:"$PWD/logs"
```

Green on all three. The unique index takes, and the leftover checkpoint is **deleted** on the way through — Module 0's punchline, confirmed again: a green run leaves no checkpoint.

## What each folder is

| Path | Purpose |
| --- | --- |
| `baseline/` | Healthy `Shop` + the run-once seed with two duplicate emails, under a non-unique `IX_Customer_Email`. Deploys green. |
| `after/` | Baseline with `IX_Customer_Email` flipped to **unique** — the dup-key incident. Recovery is a data dedupe + redeploy. |
| `quench.settings.<state>.json` | One per package, both targeting `diag_blackbox`, lab-local `artifacts`/`checkpoints`. |

That's the whole method: **locate** the failing phase from the `FAILED to quench:` block, **read** the engine's error, **recover**. Next up — when the fix isn't a one-line `UPDATE`, the recovery *toolkit* (`--ResumeQuench`, marking a script done) is **Module 5**; the full anatomy of index, constraint & FK failures is **Module 3**.
