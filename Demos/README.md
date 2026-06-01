# SchemaSmith Community Demos

Demo database schema packages for SQL Server, PostgreSQL, and MySQL, deployed by [SchemaSmith](https://github.com/Schema-Smith/SchemaSmith) (SchemaQuench).

## Demo Products

| Product | SQL Server | PostgreSQL | MySQL |
|---------|:---:|:---:|:---:|
| AdventureWorks | Done | Done | Done |
| Chinook | Done | Done | Done |
| Northwind | Done | Done | Done |
| Sakila | Done | Done | Done |

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

## Sources & Licensing

Each product folder contains a `PROVENANCE.md` documenting the canonical source, license, and extraction notes. All demo products are extracted from open-source sample databases using the SchemaSmith toolset.

## Additional Resources

- [SchemaSmith Website](https://schemasmith.com) -- documentation and getting started guides
