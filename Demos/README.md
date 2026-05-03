# SchemaSmith Community Demos

Demo database schema packages for SQL Server, PostgreSQL, and MySQL, deployed by [SchemaSmith](https://github.com/Schema-Smith/SchemaSmith) (SchemaQuench).

## Demo Products

| Product | SQL Server | PostgreSQL | MySQL |
|---------|:---:|:---:|:---:|
| ValidProduct | Done | Done | Done |
| AdventureWorks | Done | Done | Done |
| Sakila | Done | Done | Done |
| Northwind | Done | Done | Done |
| Chinook | Done | Done | Done |

## Quick Start

Choose a platform and run:

```bash
# SQL Server
cd SqlServer && docker compose pull && docker compose build && docker compose up

# PostgreSQL
cd PostgreSQL && docker compose pull && docker compose up

# MySQL
cd MySQL && docker compose pull && docker compose up
```

Each platform folder contains:
- Demo schema packages (product folders)
- A `docker-compose.yml` that spins up the database server and runs SchemaQuench to deploy the demo schemas
- A `.env` file with default credentials

SQL Server requires `docker compose build` because it uses a custom Dockerfile to install Full-Text Search.

## Sources & Licensing

Each product folder contains a `PROVENANCE.md` documenting the canonical source, license, and extraction notes. All demo products are extracted from open-source sample databases using the SchemaSmith toolset.

## Additional Resources

- [SchemaSmith Website](https://schemasmith.com) -- documentation and getting started guides
