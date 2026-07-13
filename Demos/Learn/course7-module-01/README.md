# Course 7, Module 1 — Database-per-tenant fan-out (lab)

Goal: deploy one product schema — the four-table `Shop` — across a **fleet** of tenant databases in a
single run. You won't name the databases in the package. Instead the template's
`DatabaseIdentificationScript` asks each engine's catalog *"who's in the fleet?"* and SchemaSmith forges
the schema into every tenant it finds. Then you'll onboard a brand-new tenant without touching the
package at all. All three engines.

This is the **database axis** of fan-out — many databases of the same engine. (Course 2, Module 2
covered the *schema* axis — many schemas inside one database. Different axis; don't confuse them.)

## Before you start

- The [sandbox](../docker) is up and verified (all three engines healthy).
- The fleet exists — run [`../course7-setup`](../course7-setup) once (creates `fleet_tenant_001`…`005`,
  **empty**, on each engine).
- The CLI is on your PATH — `schemaquench --version` answers **2.3.0** or later. New to the CLI?
  Course 1, Module 1 walks the install.

Each engine folder (`sqlserver/`, `postgres/`, `mysql/`) ships a `quench.settings.json` (the deploy
config) and the `Package/` — the same `Shop` package, three native forms.

## Step 1: See who's in the fleet

The whole module turns on one line in `sqlserver/Package/Templates/Main/Template.json`:

```json
"DatabaseIdentificationScript": "SELECT [Name] FROM sys.databases WHERE [Name] LIKE 'fleet[_]tenant[_]%'"
```

That query — run against the server's catalog — *is* the roster. Preview it without deploying anything:

```bash
cd sqlserver
schemaquench --ConfigFile:quench.settings.json --PreviewTargets
```

```
Locate Databases To Quench (localhost,11433)
Template: Main [required]
  db: fleet_tenant_001
  db: fleet_tenant_002
  db: fleet_tenant_003
  db: fleet_tenant_004
  db: fleet_tenant_005
```

Five tenants, discovered live. No database name appears anywhere in the package.

## Step 2: Deploy — one run, the whole fleet

```bash
schemaquench --ConfigFile:quench.settings.json
```

One work unit per tenant, dispatched together:

```
[localhost,11433].[fleet_tenant_001] Dispatching work unit (source: db=DatabaseIdentificationScript, schema=(regular template))
[localhost,11433].[fleet_tenant_002] Dispatching work unit (source: db=DatabaseIdentificationScript, schema=(regular template))
[localhost,11433].[fleet_tenant_003] Dispatching work unit (source: db=DatabaseIdentificationScript, schema=(regular template))
[localhost,11433].[fleet_tenant_004] Dispatching work unit (source: db=DatabaseIdentificationScript, schema=(regular template))
[localhost,11433].[fleet_tenant_005] Dispatching work unit (source: db=DatabaseIdentificationScript, schema=(regular template))
```

Confirm the `Shop` schema landed in every tenant:

```bash
for n in 001 002 003 004 005; do
  docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 \
    -Q "SELECT '$n', COUNT(*) FROM fleet_tenant_$n.sys.tables WHERE name IN ('Customer','Product','SalesOrder','OrderItem')"
done
# → each tenant reports 4
```

## Step 3: Prove idempotence — run it again

```bash
schemaquench --ConfigFile:quench.settings.json
```

Nothing changes. SchemaSmith reconciles each tenant to the declared shape; a fleet already in shape is a
five-way no-op. Re-running the whole fleet is always safe.

## Step 4: Onboard a new tenant — no package change

A new customer signs up. Stand up their database:

```bash
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C \
  -Q "CREATE DATABASE [fleet_tenant_006]"
```

Deploy again — you edit nothing:

```bash
schemaquench --ConfigFile:quench.settings.json
```

Discovery now returns **six**. Only `fleet_tenant_006` is forged; `001`–`005` are no-ops. The roster is
the catalog, so onboarding is a `CREATE DATABASE`, not a package edit. (Clean up with
`DROP DATABASE [fleet_tenant_006]` when you're done, to return to the five-tenant baseline.)

## Step 5: Do it on PostgreSQL and MySQL

Same five steps in `postgres/` and `mysql/`. The package is native per engine, and only the catalog the
`DatabaseIdentificationScript` reads differs:

| | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| Catalog | `sys.databases` | `pg_database` | `information_schema.schemata` |
| Discovery predicate | `[Name] LIKE 'fleet[_]tenant[_]%'` | `datname LIKE 'fleet\_tenant\_%'` | `SCHEMA_NAME LIKE 'fleet\_tenant\_%'` |

Each one fans the `Shop` schema across all five tenants, re-runs to a clean no-op, and onboards a sixth
the moment its database exists.

## The principle

At scale, you don't deploy *a* database — you deploy *a roster*. The package is canonical and unchanging;
the fleet it reaches is whatever the catalog reports at run time. Adding a tenant is a data change, not a
release. That's the shift from authoring a schema to operating a fleet.

Next: **Module 2** hands the roster to your *deployment system* instead of the catalog — when the list of
tenants to touch should come from config (and a subset), not every database that happens to match a name.
