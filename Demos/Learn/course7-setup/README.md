# Course 7 — Database Setup

These scripts stand up the **tenant fleet** that the Course 7 labs deploy into: five **empty**
databases per engine — `fleet_tenant_001` through `fleet_tenant_005` — on SQL Server, PostgreSQL,
MySQL, and MariaDB (20 databases in all).

Nothing is seeded into them. The Module 1 deploy is what forges the `Shop` schema (`Customer`,
`Product`, `SalesOrder`, `OrderItem`) into every tenant in a single fan-out run — that is the whole
point of the module.

The `fleet_tenant_` prefix is deliberately distinct from Course 6's `shop_tenant_a/b/c`, which share
the same sandbox. Course 7 discovers its fleet with a catalog query on that prefix, so keeping the
names apart means the fan-out never accidentally reaches into another course's databases.

## Prerequisite

The shared sandbox must be running. See [`Demos/Learn/README.md`](../README.md) for how to start it
and verify it is healthy before continuing.

## Run the setup

**macOS / Linux**

```bash
cd Demos/Learn/course7-setup
bash setup-databases.sh
```

**Windows (PowerShell)**

```powershell
cd Demos\Learn\course7-setup
.\setup-databases.ps1
```

Both scripts print `PASS` or `FAIL` per engine (`PASS` only after all five tenant databases are
confirmed to exist), and finish with a one-line summary. Re-running is safe — every `CREATE` is
guarded, so setup is idempotent.

## What each script does

| File | Purpose |
| --- | --- |
| [`seed/sqlserver/01_create_tenant_databases.sql`](seed/sqlserver/01_create_tenant_databases.sql) | `CREATE DATABASE fleet_tenant_001..005` (guarded on `DB_ID`). |
| [`seed/postgres/01_create_tenant_databases.sql`](seed/postgres/01_create_tenant_databases.sql) | Generates + `\gexec`s the missing `CREATE DATABASE` statements (PostgreSQL has no `IF NOT EXISTS` for databases). |
| [`seed/mysql/01_create_tenant_databases.sql`](seed/mysql/01_create_tenant_databases.sql) | `CREATE DATABASE IF NOT EXISTS fleet_tenant_001..005`. |
| [`seed/mariadb/01_create_tenant_databases.sql`](seed/mariadb/01_create_tenant_databases.sql) | `CREATE DATABASE IF NOT EXISTS fleet_tenant_001..005` (MariaDB native package). |

## Verify by hand

Each engine's catalog is the fleet roster — the same query Module 1's `DatabaseIdentificationScript`
runs. To confirm the five tenants are present:

```bash
# SQL Server
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C \
  -Q "SELECT name FROM sys.databases WHERE name LIKE 'fleet[_]tenant[_]%' ORDER BY name"

# PostgreSQL
docker exec learn-postgres psql -U postgres -c \
  "SELECT datname FROM pg_database WHERE datname LIKE 'fleet\_tenant\_%' ORDER BY datname"

# MySQL
docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e \
  "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'fleet\_tenant\_%' ORDER BY schema_name"

# MariaDB
docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -e \
  "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'fleet\_tenant\_%' ORDER BY schema_name"
```

Onboarding a sixth tenant later (Module 1 demonstrates this) is just another `CREATE DATABASE
fleet_tenant_006` — the next deploy discovers it automatically.
