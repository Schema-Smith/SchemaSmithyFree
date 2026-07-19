# SchemaSmith Community Demos

Demo database schema packages for SQL Server, PostgreSQL, and MySQL, deployed by [SchemaSmith](https://github.com/Schema-Smith/SchemaSmith) (SchemaQuench).

## Demo Products

| Product | SQL Server | PostgreSQL | MySQL |
|---------|:---:|:---:|:---:|
| AdventureWorks | Done | Done | Done |
| Chinook | Done | Done | Done |
| Northwind | Done | Done | Done |
| Sakila | Done | Done | Done |
| TenantCRM | Done | Done | n/a — schema templates are SQL Server + PostgreSQL only |

TenantCRM is a hand-authored multi-tenant CRM showcasing the **schema-per-tenant** pattern — one schema-template definition fanned out across an arbitrary number of tenant schemas inside a single database. See the [SQL Server](SqlServer/TenantCRM/README.md) or [PostgreSQL](PostgreSQL/TenantCRM/README.md) demo READMEs for the walkthrough.

## Conditional Deployment Demos

Demonstrations of `ShouldApplyExpression` (conditional deployment) on real engine pairs, supporting the *Production Server That Can't Be Upgraded* article (LinkedIn, 2026-06-11). See [`Conditional/`](Conditional/) for the three demos:

- [`Conditional/PostgreSQL-VersionGate`](Conditional/PostgreSQL-VersionGate) — PG15 ↔ PG18, gating a virtual generated column on PG18+
- [`Conditional/SqlServer-RollingRollout`](Conditional/SqlServer-RollingRollout) — SQL Server 2022 × 3 tenant databases, rolling out a nonclustered columnstore index one tenant per maintenance window via a `RolloutControl` table
- [`Conditional/MySQL-VersionGate`](Conditional/MySQL-VersionGate) — MySQL 8.0 ↔ MySQL 9, gating a `VECTOR(384)` column on MySQL 9+

These demos use a different docker layout than the products above (engine pairs rather than a single instance) and live in `Conditional/` rather than per-platform subdirectories.

## Quick Start

Choose a platform and run the matching launcher.

Windows (cmd):

```cmd
:: SQL Server
cd SqlServer && run-demo.cmd

:: PostgreSQL
cd PostgreSQL && run-demo.cmd

:: MySQL
cd MySQL && run-demo.cmd
```

macOS / Linux (bash):

```bash
# SQL Server
cd SqlServer && ./run-demo.sh

# PostgreSQL
cd PostgreSQL && ./run-demo.sh

# MySQL
cd MySQL && ./run-demo.sh
```

The launcher publishes SchemaQuench from source (via `build-schemaquench.cmd` / `.sh` — requires the .NET SDK on the host), then runs `docker compose up --build -d` to start the database server and deploy the demo schemas.

Each platform folder contains:
- Demo schema packages (product folders)
- A `docker-compose.yml` that spins up the database server and runs SchemaQuench to deploy the demo schemas
- A `run-demo.sh` / `run-demo.cmd` launcher
- A `.env` file with default credentials

### SQL Server version (optional)

The SQL Server demo builds on `mcr.microsoft.com/mssql/server:2022-latest` by default. A fresh SQL Server container runs a one-time system-database upgrade on first boot — a few seconds on a fast disk, but many minutes on a slow or resource-constrained Docker backend, and the amount of work varies a lot by version. Point the demo at a different image with `MSSQL_IMAGE` in [`SqlServer/.env`](SqlServer/.env) (the [Learn sandbox](Learn/docker) honors the same variable):

| `MSSQL_IMAGE` | First boot | Pick it when |
| --- | --- | --- |
| `mcr.microsoft.com/mssql/server:2019-latest` | fastest (~⅓ the upgrade work of 2022) | your Docker backend is slow, or you run SQL Server 2019 |
| `mcr.microsoft.com/mssql/server:2022-latest` *(default)* | slowest | you run SQL Server 2022 |
| `mcr.microsoft.com/mssql/server:2025-latest` | ~½ of 2022 | you want the current release, or you run SQL Server 2025 |

All three are tested end-to-end — every demo package, including the AdventureWorks full-text catalog and indexes, deploys cleanly.

### Run on your own SQL Server (no Docker)

Already have a SQL Server? Skip Docker and deploy the demo databases straight onto your instance with `SqlServer/deploy-to-endpoint.ps1` (Windows) or `deploy-to-endpoint.sh` (macOS/Linux). It resets and redeploys the same demo set behind a confirmation, and refuses to touch any same-named database it didn't create. Requires the `sqlcmd` client on your `PATH`. Full walkthrough: [Use your own server](../docs/end-user/guide/use-your-own-server.md).

## Sources & Licensing

Each extracted product folder contains a `PROVENANCE.md` documenting the canonical source, license, and extraction notes. AdventureWorks, Chinook, Northwind, and Sakila are extracted from open-source sample databases using the SchemaSmith toolset. TenantCRM is hand-authored as a schema-template feature demo and has a tutorial `README.md` in place of a `PROVENANCE.md`.

## Additional Resources

- [SchemaSmith Website](https://schemasmith.com) -- documentation and getting started guides
