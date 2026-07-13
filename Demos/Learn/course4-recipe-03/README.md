# Course 4, Recipe 3 — The package asks the server (lab)

Goal: resolve a token from a **live query against the target database** at deploy time, before any of your
scripts run. The package reads which feature flags are switched on *right now* on the server it's deploying
to, and records them — a value that only exists on the target, pulled in at the moment of the quench.

Each engine folder (`sqlserver/`, `postgres/`, `mysql/`) ships the full `Package/`, a `seed-server.sql`, and
`deploy.settings.json`, all targeting `cookbook_r3`.

## Before you start

- The [sandbox](../docker) is up and the Course 4 databases exist (run [`../course4-setup`](../course4-setup) once — it creates `cookbook_r3`).
- The CLI is on your PATH (`schemaquench --version` → `2.3.0.0` or later).

## Step 1: Seed the server's state

`seed-server.sql` creates a `FeatureFlag` table and switches on `Billing` and `Reporting`, leaving `BetaSearch`
off. This stands in for state an app or an operator already put on the server — it's **not** part of your
schema package.

```bash
# SQL Server
docker exec -i learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d cookbook_r3" < sqlserver/seed-server.sql
```

## Step 2: Look at the query token

In `Templates/Main/Template.json`, a token is defined with the `<*Query*>` tag:

```json
"ScriptTokens": {
  "EnabledFeatures": "<*Query*>SELECT STRING_AGG(FlagName, ',') ... FROM FeatureFlag WHERE Enabled = 1"
}
```

It lives on the **template** (not the product) so it resolves against the *target* database — the one your
`DatabaseIdentificationScript` selected — rather than the server's default database. The `[ALWAYS]` after-script
`Record Active Features [ALWAYS].sql` then just references `{{EnabledFeatures}}` and writes it to a `DeployLog`
row. `[ALWAYS]` means it runs on every quench, not once.

## Step 3: Deploy — the package reads the server

```bash
cd <engine>
schemaquench --ConfigFile:deploy.settings.json
```

SchemaSmith runs the query against `cookbook_r3` just before the script, substitutes the result, and the
after-script records it:

```bash
# SQL Server
docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d cookbook_r3 -Q \"SELECT ActiveFeatures FROM dbo.DeployLog\""
# → Billing,Reporting
```

## Step 4: Change the server, re-deploy — the value follows

Switch on `BetaSearch` on the server and quench again:

```bash
# PostgreSQL
docker exec learn-postgres psql -U postgres -d cookbook_r3 -c "UPDATE public.featureflag SET enabled=true WHERE flagname='BetaSearch'"
schemaquench --ConfigFile:deploy.settings.json
docker exec learn-postgres psql -U postgres -d cookbook_r3 -tAc "SELECT activefeatures FROM public.deploylog ORDER BY loggedat"
# → Billing,Reporting
#   BetaSearch,Billing,Reporting
```

The second deploy re-ran the query against the live server and recorded the new feature set. Nothing in the
package changed — the package asked the server, and the server's answer had changed.

## Per-engine notes

| | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| Aggregate in the query | `STRING_AGG(... ) WITHIN GROUP (ORDER BY ...)` | `STRING_AGG(..., ',' ORDER BY ...)` | `GROUP_CONCAT(... ORDER BY ... SEPARATOR ',')` |
| Token scope | template-level (resolves against the target DB) | same | same |
| Deploy-log timestamp default | `SYSUTCDATETIME()` | `clock_timestamp()` | `CURRENT_TIMESTAMP(6)` |

The `<*Query*>` mechanism is identical on all three engines; only the aggregate function's dialect differs.

## The principle

Some values can't be baked into a package because they only exist on the target — which tenants are active,
which features are on, the next batch number. `<*Query*>` lets the package *ask* the server at deploy time and
fold the answer into a token. And if the query can't run — wrong table, no permission — the deploy stops up
front with a clear error, before a single change is applied. Safe by default.
