# SchemaSmithy Documentation

SchemaSmithyFree is a SQL Server database schema management toolset that enables database-as-code workflows. The desired database state is defined as metadata, and the tools transform any target server to match that state — similar in concept to HashiCorp's Terraform, but for SQL Server databases.

---

## Tools

- **[SchemaQuench](schemaquench/README.md)** — Applies schema packages to SQL Server databases. Takes the desired end state and transforms the target server to match.
- **[SchemaTongs](schematongs/README.md)** — Extracts database schema from a live SQL Server into schema package format.
- **[DataTongs](datatongs/README.md)** — Extracts table data from a live SQL Server and generates MERGE scripts for data synchronization.
- **[SchemaHammer](schemahammer/README.md)** — Desktop schema viewer for browsing schema packages visually. Read-only viewer with tree navigation, search, and SQL syntax highlighting.

---

## Concepts

| Topic | Description |
|-------|-------------|
| [Getting Started](getting-started.md) | Installation, prerequisites, first run |
| [Schema Packages](schema-packages.md) | Folder structure and package conventions |
| [Products and Templates](products-and-templates.md) | Product.json, Template.json, script folders, quench slots |
| [Defining Tables](defining-tables.md) | Table JSON format reference |
| [Script Tokens](script-tokens.md) | Token syntax and resolution |
| [CLI Options](cli-options.md) | Shared command-line switches |
| [Logging](logging.md) | Log files, backup, and exit codes |
| [Complete Feature List](complete-feature-list.md) | Comprehensive feature inventory |

---

## Quick Start

With Docker installed, the tools can be run from the repository root:

```bash
docker compose build
docker compose up
```

This applies the included test product to a Linux SQL Server 2019 container. Connect to the server at `localhost` with the credentials defined in `.env`.

For more samples, see the [demo repository](https://github.com/Schema-Smith/SchemaSmithDemos). For full documentation, visit [schemasmith.com](https://schemasmith.com/documentation/mssql/community/getting-started.html).
