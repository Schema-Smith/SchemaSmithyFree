# SchemaSmith Community Demos

Demo database schema packages for SQL Server, PostgreSQL, and MySQL, deployed by [SchemaSmith](https://github.com/Schema-Smith/SchemaSmith) (SchemaQuench).

## Demo Products

| Product | SQL Server | PostgreSQL | MySQL |
|---------|:---:|:---:|:---:|
| AdventureWorks | Done | Done | Done |
| Chinook | Done | Done | Done |
| Northwind | Done | Done | Done |
| Sakila | Done | Done | Done |

## Quick Start

Choose a platform and run the matching launcher:

```bash
# SQL Server
cd SqlServer && ./run-demo.sh

# PostgreSQL
cd PostgreSQL && ./run-demo.sh

# MySQL
cd MySQL && ./run-demo.sh
```

On Windows, use `run-demo.cmd` in place of `run-demo.sh`. The launcher publishes SchemaQuench from source (via `build-schemaquench.sh` / `.cmd` — requires the .NET SDK on the host), then runs `docker compose up --build -d` to start the database server and deploy the demo schemas.

Each platform folder contains:
- Demo schema packages (product folders)
- A `docker-compose.yml` that spins up the database server and runs SchemaQuench to deploy the demo schemas
- A `run-demo.sh` / `run-demo.cmd` launcher
- A `.env` file with default credentials

## Sources & Licensing

Each product folder contains a `PROVENANCE.md` documenting the canonical source, license, and extraction notes. All demo products are extracted from open-source sample databases using the SchemaSmith toolset.

## Additional Resources

- [SchemaSmith Website](https://schemasmith.com) -- documentation and getting started guides
