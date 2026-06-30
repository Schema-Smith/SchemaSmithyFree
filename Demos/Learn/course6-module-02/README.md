# Course 6, Module 2 — Pre-flight diagnostics: `--TestConnection` and `--PreviewTargets` (lab)

<!-- TRAINING-RELEASE-PIN #312 — this lab uses --TestConnection / --PreviewTargets, which ship in the release after v2.1.0. Certified against from-source `main`. At that release: bump the `schemaquench --version` pre-flight note and remove this sentinel + the release-coupled roadmap row. -->

**Goal:** run SchemaQuench's two read-only pre-flight switches to validate a deployment before committing to the maintenance window — confirming connectivity, the minimum-version floor, and the exact target roster, then wire both checks as a CI gate that aborts on failure.

## The scenario

Your team is about to push a schema update to three tenant databases across a fleet. Before you open the maintenance window, you want hard answers to two questions: can SchemaQuench actually reach the servers and do they meet the product's declared minimum version? And will the target-discovery query find the right set of databases — no more, no fewer? Both questions can be answered now, without deploying a byte, using `--TestConnection` and `--PreviewTargets`. This lab walks through each switch, shows you what failure looks like (and how to trigger it deliberately), and ends with a CI gate pattern you can drop into any pipeline.

## Before you start

- The three-engine sandbox is up (`Demos/Learn/docker`) and `course6-setup` has been run — it seeds `shop_tenant_a`, `shop_tenant_b`, and `shop_tenant_c` on each engine.
- `schemaquench --version` answers on your PATH, **built from `main`**. The `--TestConnection` and `--PreviewTargets` switches are not in stock v2.1.0 — they ship in the release after v2.1.0 (#312). If `schemaquench --help` does not list them, rebuild from `main`.

## Scenario 1 — `--TestConnection` pass

`--TestConnection` connects to every configured server, checks that the detected version meets `Product.json`'s `MinimumVersion`, and exits. Exit code 0 means the deployment is clear to proceed on connectivity grounds; exit code 2 means at least one server failed the check.

Run from any of the three engine directories:

```
schemaquench --ConfigFile:quench.settings.json --TestConnection
```

SQL Server output:

```
Pre-flight diagnostics for Shop (--TestConnection)
Testing connection to configured servers
  localhost,11433 (a742c1f6fc50) connection succeeded

Validate Minimum Version
RESULT: PASS (connections and minimum version validated)
```

Exit code: `0`. PostgreSQL and MySQL produce the same shape (`connection succeeded` line, then `RESULT: PASS`) with their respective connection strings.

## Scenario 2 — `--TestConnection` fail: raise the floor above the server

This scenario shows what the check looks like when the server is reachable but below the declared floor — a realistic situation when a product ships a version requirement the target environment hasn't been upgraded to meet.

**Edit `Package/Product.json`** in the engine directory you're using. Raise `MinimumVersion` above the actual server version:

- SQL Server: set `"MinimumVersion": "99"`
- PostgreSQL: set `"MinimumVersion": "99"`
- MySQL: set `"MinimumVersion": "9.9"`

Re-run:

```
schemaquench --ConfigFile:quench.settings.json --TestConnection
```

SQL Server output (floor `99`, detected version `16`):

```
Validate Minimum Version
Pre-flight FAILED: One or more target servers are below the product's declared MinimumVersion; aborting before any deployment:
  localhost,11433: detected version 16 is below the product's declared MinimumVersion 99
```

PostgreSQL output (floor `99`, detected version `160013`):

```
  localhost: detected version 160013 is below the product's declared MinimumVersion 99
```

MySQL output (floor `9.9`, detected version `8.0.45`):

```
  localhost: detected version 8.0.45 is below the product's declared MinimumVersion 9.9
```

Exit code: `2` on all three engines. The manifest names the server, the detected version, and the declared floor — enough information to act without opening a separate database client.

**Restore `MinimumVersion`** to its original value before continuing (`2019` for SQL Server, `15` for PostgreSQL, `8.0` for MySQL).

## Scenario 3 — `--PreviewTargets` pass

`--PreviewTargets` runs the full target-discovery phase — connects, validates the version floor, evaluates the `DatabaseIdentificationScript` for each template, and prints the resolved target tree — without deploying anything. Exit code 0 means discovery succeeded and at least one target was found for each required template.

```
schemaquench --ConfigFile:quench.settings.json --PreviewTargets
```

SQL Server output:

```
Pre-flight diagnostics for Shop (--PreviewTargets)
Testing connection to configured servers
  localhost,11433 (a742c1f6fc50) connection succeeded

Validate Minimum Version
Load Template Schema: Main
Check for Template Special Script Tokens
Locate Databases To Quench (localhost,11433)
Template: Main [required]
  db: shop_tenant_a
  db: shop_tenant_b
  db: shop_tenant_c
RESULT: PASS
```

Exit code: `0`. PostgreSQL and MySQL produce the same target tree (three tenant databases under `Template: Main [required]`), exit 0.

## Scenario 4 — `--PreviewTargets` fail: point discovery at a pattern that matches nothing

This scenario shows what happens when the `DatabaseIdentificationScript` would resolve zero targets for a required template — for example, if the naming convention drifted or the wrong environment was targeted.

**Edit `Package/Templates/Main/Template.json`** in the engine directory you're using. Change the LIKE pattern in `DatabaseIdentificationScript` to `shop_tenant_z%` (matches nothing in any engine's sandbox). `RequireAtLeastOneTarget` defaults to `true`, so an empty match is a failure.

SQL Server `Template.json` before the change:

```json
"DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] LIKE 'shop_tenant_%'"
```

Change it to:

```json
"DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] LIKE 'shop_tenant_z%'"
```

Apply the equivalent change to the PostgreSQL or MySQL `Template.json` if you're running those engines. Re-run:

```
schemaquench --ConfigFile:quench.settings.json --PreviewTargets
```

Output (identical on all three engines):

```
Locate Databases To Quench (localhost,11433)
Template: Main [required]
  ERROR: matched 0 targets for required template 'Main' - no databases or schemas were discovered
RESULT: FAIL (one or more required templates matched nothing)
```

Exit code: `2`. The error names the template and the failure reason — useful when a pattern is correct in one environment and silently wrong in another.

**Restore the original LIKE pattern** (`shop_tenant_%`) before continuing.

## Scenario 5 — CI readiness gate

Both switches return exit code 0 on success and 2 on failure, which maps directly onto CI gate patterns. The typical pipeline runs `--TestConnection` first, then `--PreviewTargets`, and aborts on the first non-zero exit rather than running both to completion.

**Bash:**

```bash
schemaquench --ConfigFile:quench.settings.json --TestConnection || { echo "GATE: pre-flight failed — aborting deploy"; exit 1; }
schemaquench --ConfigFile:quench.settings.json --PreviewTargets || { echo "GATE: target preview failed — aborting deploy"; exit 1; }
echo "GATE: pre-flight green — safe to deploy"
```

**PowerShell:**

```powershell
schemaquench --ConfigFile:quench.settings.json --TestConnection
if ($LASTEXITCODE -ne 0) { Write-Error "Pre-flight failed — aborting deploy"; exit 1 }
schemaquench --ConfigFile:quench.settings.json --PreviewTargets
if ($LASTEXITCODE -ne 0) { Write-Error "Target preview failed — aborting deploy"; exit 1 }
Write-Host "Pre-flight green — safe to deploy"
```

With the lab in its passing state (restored floors, restored patterns) both snippets print the green line. Re-introducing the Scenario 2 floor bump makes `--TestConnection` exit 2 and the gate aborts before `--PreviewTargets` runs.

The exit codes — 0 and 2 — are the contract. Whether the gate is GitHub Actions, Azure Pipelines, GitLab CI, or a shell script in a deployment runbook, the pattern is the same.

## Per-engine notes

| | SQL Server | PostgreSQL | MySQL |
|---|---|---|---|
| Connection | `localhost,11433` (sa) | `localhost:15432` (postgres) | `localhost:13306` (root) |
| `DatabaseIdentificationScript` | `SELECT [Name] FROM master.sys.databases WHERE [Name] LIKE 'shop_tenant_%'` | `SELECT datname FROM pg_database WHERE datname LIKE 'shop_tenant_%'` | `SELECT SCHEMA_NAME FROM information_schema.schemata WHERE SCHEMA_NAME LIKE 'shop_tenant_%'` |
| Detected version display | `16` | `160013` | `8.0.45` |
| Pass floor used | `2019` | `15` | `8.0` |

MySQL has no schema-within-database layer, so `--PreviewTargets` lists databases only — no `schemas:` sub-lines under each database entry. On SQL Server and PostgreSQL, a schema-template package would add those lines; that pattern is out of scope for this database-level module.

## What you proved

You validated connectivity, the minimum-version floor, and the exact target roster for a fleet deployment — and you saw what each failure mode looks like and how to trigger it deliberately. Neither switch deployed anything. The exit-code contract means both checks can gate a CI pipeline directly, giving you machine-verifiable pre-flight confidence before the maintenance window opens.
