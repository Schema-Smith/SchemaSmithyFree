# SchemaSmith Community Edition

*Terraform for SQL Server databases*

![Build](https://github.com/Schema-Smith/SchemaSmithyFree/actions/workflows/continuous-integration.yml/badge.svg)
[![License: SSCL v2.0](https://img.shields.io/badge/license-SSCL%20v2.0-blue)](LICENSE)

SchemaSmith is a state-based database schema management toolset for SQL Server. Define your desired database state as metadata — tables, views, procedures, triggers — and SchemaSmith transforms any target server to match.

## Why State-Based?

Migrations show the evolution of a database over time, but you can't tell what the current state is at a glance. With a state-based approach, your metadata repository is an exact representation of what your server should be — treating SQL Server code like any other production code, guaranteeing they are always in sync.

## Tools

- **SchemaQuench** — Deploys schema packages to databases
- **SchemaTongs** — Extracts database schemas into schema packages
- **DataTongs** — Extracts table data and generates MERGE scripts

## Quick Start

If you have Docker, run from the project root:

```bash
docker compose build
docker compose up
```

This applies the [Test Product](TestProducts/ValidProduct/Product.json) to a SQL Server container. Connect at `localhost` with credentials from [.env](.env).

For more samples, see the [SchemaSmithDemos](https://github.com/Schema-Smith/SchemaSmithDemos) repository.

## Technical Notes

- **Target Frameworks**: net9.0, net481
- **IDEs**: Visual Studio 2022 or JetBrains Rider
- **Database**: Tested against SQL Server 2019-CU27. Should work with any version at compatibility level 130 or higher.

## License

SchemaSmith Community Edition is licensed under [SSCL v2.0](LICENSE). No organization size or revenue restrictions — use for any purpose, personal or commercial. Tiers are feature-based only: Community is free, Enterprise adds multi-platform support and advanced features.

## Additional Resources

- [Documentation](https://schemasmith.com/documentation/mssql/community/getting-started.html)
- [Community Command Center](https://github.com/Schema-Smith/Community) — roadmap, plans, feature matrices
