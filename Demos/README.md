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

Each extracted product folder contains a `PROVENANCE.md` documenting the canonical source, license, and extraction notes. AdventureWorks, Chinook, Northwind, and Sakila are extracted from open-source sample databases using the SchemaSmith toolset. TenantCRM is hand-authored as a schema-template feature demo and has a tutorial `README.md` in place of a `PROVENANCE.md`.

## Additional Resources

- [SchemaSmith Website](https://schemasmith.com) -- documentation and getting started guides
