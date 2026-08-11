# Course 7, Module 6 — Reading the deployment summary report (lab)

Goal: roll a real, mixed change across the whole fleet — a new table, a new unique index, a widened column, a new column, a dropped index, and a re-applied view — with one tenant pre-seeded to fail, then read the run's structured receipt end to end. You'll pull apart `SchemaQuench - Summary.json`: the run verdict, every target's outcome, where the milliseconds went, the failure as data, and — the centerpiece — the verified `objectChanges` audit. SQL Server, then PostgreSQL, MySQL, and MariaDB.

This is the reporting capstone of Course 7. Module 5 taught you to *hunt* a failure through `Failures.log`, checkpoints, and artifacts. Module 6 opens the one structured file that names every target's fate and every verified change in a single read — the file a dashboard or a release gate parses without a human scrolling logs.

## Before you start

> **Engine floor:** on your own server this lab needs **SQL Server 2016+** (it uses `CREATE OR ALTER`). Other engines run it at any supported version. The Docker sandbox is already above the floor.

- The [sandbox](../docker) is up and verified (all four engines healthy).
- The fleet exists — run [`../course7-setup`](../course7-setup) once if you haven't already (`fleet_tenant_001`–`005` on every engine).
- **The CLI is on your PATH** — `schemaquench --version` answers **2.3.0** or later.

Each engine folder ships a `baseline/` package (the established fleet state, including an `IX_Customer_FullName` index the rollout will drop), an `after/` package (the mixed rollout), two settings files, and a drift + reset helper for the one tenant staged to fail.

| File | What it's for |
| --- | --- |
| `baseline/` | The established Shop package — has `IX_Customer_FullName`, no `ShipmentEvent`, no `UQ_Customer_Email`. |
| `after/` | The rollout: adds `ShipmentEvent` + `UQ_Customer_Email`, widens `Customer.FullName`, adds `Customer.Region`, drops `IX_Customer_FullName`, re-applies the `vw_ActiveProducts` view. |
| `quench.settings.baseline.json` | Fleet run against `baseline/`. |
| `quench.settings.after.json` | Fleet run against `after/` (wires `ArtifactPath`). |
| `drift-tenant-003.sql` | Plants duplicate emails in `fleet_tenant_003` so its `UQ_Customer_Email` can't build. |
| `reset-tenant-003.sql` | Clears the duplicates so a re-run converges to an all-Success summary. |

## Step 1: Deploy the baseline

Land the `baseline/` package fleet-wide so every tenant starts at the same known state:

```bash
cd sqlserver
schemaquench --ConfigFile:quench.settings.baseline.json
```

All five tenants deploy cleanly, exit `0`.

## Step 2: Stage the one failure

Plant duplicate emails in a single tenant so the rollout's new unique index fails there — and nowhere else:

```bash
cd ..            # back to the lab folder
../lab-sql.sh sqlserver fleet_tenant_003 --file sqlserver/drift-tenant-003.sql
cd sqlserver     # back into the engine folder
```

## Step 3: Run the rollout and write the report

Deploy `after/` fleet-wide, and pin the report where the sandbox can read it. `--report` takes a base path with no extension — SchemaSmith writes both `.json` and `.md`. The lab lowers `BottleneckThresholdMs` far below its 30-second default so a fast five-tenant run still surfaces a long pole:

```bash
schemaquench --ConfigFile:quench.settings.after.json --report ./out/deploy-summary --BottleneckThresholdMs=800
```

The run finishes with exit code `2` — four tenants converge, `fleet_tenant_003` fails on its duplicate emails. Now open `out/deploy-summary.json` (and its human-readable twin `out/deploy-summary.md`).

## Step 4: Read the receipt

Read the report top to bottom — it's built to be read in that order:

- **`run`** — `outcome: "PartialFailure"`, `exitCode: 2`. The whole-fleet verdict as fields you can branch on, not a string to grep.
- **`targets[]`** — five entries; four `Success`, `fleet_tenant_003` `Failed`. Filter on `outcome != "Success"` for your failed set.
- **`timing`** — `bySlot` rolls per-slot time fleet-wide (watch the `targetCount`: `ModifiedTables` reaches all 5, the later slots only the 4 that got past the failure); `bottlenecks` lists the slot-on-a-target measurements over your threshold.
- **`failures[]`** — one entry: the same duplicate-key error, phase trail, and artifact pointer Module 5 chased through `Failures.log`, now as structured data.
- **`objectChanges`** — the centerpiece. `instrumented: true` means the counts are real. Read them as **fleet-wide totals**: on SQL Server this run reports `created` tables `5` / indexes `4` / constraints `4`, `modified.columns 5`, `dropped.indexes 5`, and `scriptsRan 5`. The two `created` buckets that read `4` instead of `5` (`UQ_Customer_Email`, `PK_ShipmentEvent`) are short by exactly the one tenant that died at the index phase — the counts name the failure before you read a single error. Note `created.views` stays `0` even though the view ran on every tenant: an object script is reported as **`scriptsRan`** / `"action": "ran"`, never "created", because a re-applied script can't be known to have changed anything. The new `Customer.Region` column has no `created.columns` bucket to roll into — it surfaces only in `details[]`; `modified.columns` counts ALTERs of existing columns (here `FullName`'s widening).

## Step 5 (optional): clear the failure and re-read

Reset the one tenant and re-run to watch the summary go all-green:

```bash
cd ..            # back to the lab folder
../lab-sql.sh sqlserver fleet_tenant_003 --file sqlserver/reset-tenant-003.sql
cd sqlserver     # back into the engine folder
schemaquench --ConfigFile:quench.settings.after.json --report ./out/deploy-summary --BottleneckThresholdMs=800
```

Exit `0`, `outcome: "Success"`, empty `failures[]`, and `objectChanges` now shows only what this second run changed — the structure already converged, so the counts are near-empty except the view, which re-runs every time (`scriptsRan`). That contrast — a busy first run, a quiet idempotent second — is the audit telling the truth about what each run actually did.

## Step 6: Do it on PostgreSQL, MySQL, and MariaDB

Same steps in `postgres/`, `mysql/`, and `mariadb/`. The report shape is identical; only the dialect in the names and errors changes. Stage the drift with each engine's client:

**PostgreSQL:**

```bash
../lab-sql.sh postgres fleet_tenant_003 --file postgres/drift-tenant-003.sql
```

**MySQL:**

```bash
../lab-sql.sh mysql fleet_tenant_003 --file mysql/drift-tenant-003.sql
```

**MariaDB:**

```bash
../lab-sql.sh mariadb fleet_tenant_003 --file mariadb/drift-tenant-003.sql
```

Then run the `after` deploy from that engine's folder with the same `--report` switch, and read its `objectChanges`. The counts move a little per engine — the phase where the failing tenant dies, and how each engine orders its object-script slot, shift a bucket or two — which is exactly the point: every run's summary is that run's own certified record, not a fixed script.

## Cleanup

The report and artifacts live under each engine folder — remove them when you're done:

```bash
rm -rf sqlserver/out sqlserver/artifacts
rm -rf postgres/out postgres/artifacts
rm -rf mysql/out mysql/artifacts
rm -rf mariadb/out mariadb/artifacts
```

To return the fleet to a clean slate for another module, re-run [`../course7-setup`](../course7-setup) after dropping the tenant databases, or deploy the `baseline/` package again.

## The principle

`Failures.log` names who broke and where — one incident, read by eye. The Deployment Summary Report names *everything*, once, as data: every target's outcome, every slot's time, every failure, and every verified change to your schema, with an honest line between what SchemaSmith confirmed against the engine and what it merely ran. Read the outcome, then the counts, then let a gap in the counts walk you straight to the target that needs a closer look.
