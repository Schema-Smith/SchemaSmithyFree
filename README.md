# SchemaSmith Community Edition

![Build](https://github.com/Schema-Smith/SchemaSmithyFree/actions/workflows/continuous-integration.yml/badge.svg)

SchemaSmith is a state-based database schema management toolset for SQL Server. Define your desired database state as metadata — tables, views, procedures, triggers — and SchemaSmith transforms any target server to match.

## Tools

- **SchemaQuench** — Applies schema packages to databases (deploy)
- **SchemaTongs** — Extracts database schemas into schema packages (extract)
- **DataTongs** — Extracts table data and generates MERGE scripts
- **SchemaHammer** — Desktop schema viewer for browsing products, templates, tables, and scripts (GUI)

## Quick Start

Build the tools and run the demo products against a local SQL Server:

```bash
docker compose -f demo/docker-compose.yml build
docker compose -f demo/docker-compose.yml up
```

This builds SchemaQuench from source, starts a SQL Server instance, and deploys the Northwind and AdventureWorks demo products. Connect to the server at `localhost:1440` with credentials from `demo/.env`.

## Building

```bash
dotnet build SchemaSmithyFree.sln
```

## Running Tests

Integration tests require a SQL Server instance on `localhost,1440`. Start one via the demo docker-compose:

```bash
docker compose -f demo/docker-compose.yml up -d demoserver
```

Then run tests:

```bash
dotnet test SchemaSmithyFree.sln
```

## Demo Products and Tutorials

See [demo/README.md](demo/README.md) for demo products, tutorials, and detailed docker-compose usage.

## Additional Resources

- [Documentation](https://schemasmith.com/documentation/mssql/community/getting-started.html)
- [Community Command Center](https://github.com/Schema-Smith/Community) — roadmap, plans, feature matrices
