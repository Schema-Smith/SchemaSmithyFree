# Course 7, Module 3 — Provisioning at onboarding (lab)

Goal: onboard a brand-new tenant in a **single deploy**. In Module 1 you ran `CREATE DATABASE` by hand
and let discovery find it. Here the config roster names a tenant that *doesn't exist yet* —
`fleet_tenant_006` — and `CreateIfMissing: true` makes SchemaSmith stand the database up itself, on the
admin connection, then forge the `Shop` schema into it. Safe by default, dry-runnable, idempotent, and
permission-gated. All three engines.

This builds on Module 2 — `CreateIfMissing` is a flag on the same `TemplateTargets` roster. Do Modules 1
and 2 first.

## Before you start

- The [sandbox](../docker) is up and verified (all three engines healthy).
- The fleet exists — run [`../course7-setup`](../course7-setup) once (creates `fleet_tenant_001`…`005`).
- `fleet_tenant_006` does **not** exist yet (that's the point). If a Module 1 run left it, drop it first.
- The CLI is on your PATH — `schemaquench --version` answers **2.2.0** or later.

Each engine folder ships the same native `Shop` `Package/` as Modules 1–2, plus three settings files —
all naming a roster of one existing tenant (`001`) and one that doesn't exist yet (`006`):

| Settings file | `CreateIfMissing` | `WhatIfONLY` | What it does |
| --- | --- | --- | --- |
| `quench.settings.skip.json` | *(absent → false)* | false | Deploys `001`; **skips** the missing `006`. |
| `quench.settings.whatif.json` | true | true | Dry-run — shows what it *would* create. |
| `quench.settings.onboard.json` | true | false | Provisions `006` and forges `Shop` in. |

## Step 1: Safe by default — a missing tenant is skipped, not created

Point at the `skip` roster. `006` doesn't exist and `CreateIfMissing` is absent, so SchemaSmith leaves it
alone:

```bash
cd sqlserver
schemaquench --ConfigFile:quench.settings.skip.json
```

```
[localhost,11433] Database 'fleet_tenant_006' does not exist and TemplateTargets CreateIfMissing is false - skipping all iterations for this server-database pair.
```

`001` deploys; `006` is skipped with that line and **not created**. The default never conjures a database
you didn't ask for.

## Step 2: Dry-run before you let it create anything

Turn `CreateIfMissing` on, but run under WhatIf first. SchemaSmith tells you exactly what it *would*
provision — and touches nothing:

```bash
schemaquench --ConfigFile:quench.settings.whatif.json
```

```
  [WhatIf] Would create database [fleet_tenant_006] (CreateIfMissing: true)
```

Check the catalog after — `006` still doesn't exist. That's your look-before-you-leap on provisioning.

## Step 3: Onboard — provision and deploy in one run

Now the real thing. `quench.settings.onboard.json` carries the roster plus `CreateIfMissing: true`:

```json
"TemplateTargets": {
  "Main": { "Databases": [ "fleet_tenant_001", "fleet_tenant_006" ], "CreateIfMissing": true }
}
```

```bash
schemaquench --ConfigFile:quench.settings.onboard.json
```

```
  Creating database [fleet_tenant_006] (CreateIfMissing: true)
```

SchemaSmith creates `fleet_tenant_006` on the admin connection, kindles the forge in it, and strikes the
whole `Shop` schema — four tables — into the fresh database. `001` is a no-op. Confirm:

```bash
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 \
  -Q "SELECT COUNT(*) FROM fleet_tenant_006.sys.tables WHERE name IN ('Customer','Product','SalesOrder','OrderItem')"
# → 4
```

Onboarding a tenant went from "run this DDL, then run the deploy" down to one deploy. Add the tenant to
the roster, quench, done.

## Step 4: Idempotent — re-run changes nothing

```bash
schemaquench --ConfigFile:quench.settings.onboard.json
```

No `Creating database` line this time. SchemaSmith's create is `IF NOT EXISTS` under the hood (a real
existence check on PostgreSQL), so a tenant that already exists is left exactly as-is. `CreateIfMissing`
is safe to leave on permanently — it only ever fills gaps.

## Step 5: Provisioning is a privileged act

Creating a database is a high-privilege operation, and SchemaSmith says so plainly when the deploy account
can't. Run the onboard as a principal that can *see* the fleet but lacks the create right, and the run
fails fast with an actionable message — it does not silently skip:

```
[localhost,11433] Database provisioning FAILED for 'fleet_tenant_006' (template 'Main'): Failed to provision database 'fleet_tenant_006'. The connecting account must have CREATE DATABASE permission on the admin database (master). Underlying error: CREATE DATABASE permission denied in database 'master'.
```

The account that onboards tenants needs the create right on the admin database. What that grant is called
differs by engine:

| Engine | Admin database | Required privilege | Underlying error you'll see |
| --- | --- | --- | --- |
| SQL Server | `master` | `CREATE DATABASE` (or `dbcreator`) | `CREATE DATABASE permission denied in database 'master'` |
| PostgreSQL | `postgres` | `CREATEDB` | `42501: permission denied to create database` |
| MySQL | `information_schema` | `CREATE` | `Access denied … to database 'fleet_tenant_006'` |

A reader/writer principal without the create right lands here — with a message that names the database and
the missing privilege, not a mystery failure.

## Step 6: Do it on PostgreSQL and MySQL

Same five steps in `postgres/` and `mysql/`. The `CreateIfMissing` config is identical on all three
engines; only the create DDL SchemaSmith emits differs (`[fleet_tenant_006]` on SQL Server,
`"fleet_tenant_006"` on PostgreSQL, `` `fleet_tenant_006` `` on MySQL). Each one skips by default,
dry-runs, provisions + forges in one run, re-runs clean, and fails fast without the create grant.

## Cleanup

Drop `fleet_tenant_006` on each engine to return to the five-tenant baseline:

```bash
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C \
  -Q "ALTER DATABASE [fleet_tenant_006] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [fleet_tenant_006]"
docker exec learn-postgres psql -U postgres -c "DROP DATABASE IF EXISTS fleet_tenant_006"
docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "DROP DATABASE IF EXISTS fleet_tenant_006"
```

## The principle

Module 1 discovered the fleet; Module 2 let config steer it; Module 3 lets config *grow* it. Onboarding a
tenant is no longer a database task plus a deploy task — it's one line in a roster and one run, with a dry
run when you want it and a clear stop when the account isn't allowed. That's the fleet operating itself.

Next: **Module 4** — running a thousand-tenant deploy safely: preview the whole roster, tune how hard it
hits, and keep going when a single tenant misbehaves.
