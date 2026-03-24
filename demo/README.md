# Demo Products and Tutorials

## Demo Products

Demo products are well-known, freely available SQL Server sample databases modified for deployment by SchemaSmith.

| Demo | Source | Status |
|------|--------|--------|
| Northwind | [Northwind pubs](https://raw.githubusercontent.com/microsoft/sql-server-samples/master/samples/databases/northwind-pubs/instnwnd.sql) | Done |
| AdventureWorks | [AdventureWorks OLTP Scripts](https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks/AdventureWorks-oltp-install-script.zip) | Done |

## Tutorials

Tutorial products are used by the [SchemaSmith website documentation](https://schemasmith.com). They demonstrate basic and multi-tenant schema package patterns.

## Quick Start

From the `demo/` directory, build and deploy all demo products:

```bash
cd demo
docker compose build
docker compose up
```

Or from the repository root:

```bash
docker compose -f demo/docker-compose.yml build
docker compose -f demo/docker-compose.yml up
```

This builds SchemaQuench from source, starts SQL Server 2022 with full-text search, creates demo databases, and deploys both Northwind and AdventureWorks.

Connect to the server at `localhost:1450` with credentials from `.env`.

## Running Just the SQL Server

If you only need a SQL Server instance (e.g., for running integration tests):

```bash
docker compose up -d demoserver
```

## Demo Product Structure

Each demo product is a standard SchemaSmith schema package:

```
ProductName/
├── .community          ← Community edition marker
├── .json-schemas/      ← Generated JSON Schema validation files
├── Product.json        ← Product definition
└── Templates/
    └── TemplateName/
        ├── Template.json
        ├── Tables/         ← Table definitions (JSON)
        ├── TableData/      ← Data merge scripts (SQL)
        ├── Views/          ← View scripts
        ├── Procedures/     ← Stored procedure scripts
        └── ...
```

## Additional Resources

- [SchemaSmith Documentation](https://schemasmith.com/documentation/mssql/community/getting-started.html)
