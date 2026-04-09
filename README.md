# SchemaSmith Community Edition

*Terraform for SQL Server databases*

![Build](https://github.com/Schema-Smith/SchemaSmithyFree/actions/workflows/continuous-integration.yml/badge.svg)
[![Latest Release](https://img.shields.io/github/v/release/Schema-Smith/SchemaSmithyFree)](https://github.com/Schema-Smith/SchemaSmithyFree/releases/latest)
[![License: SSCL v2.0](https://img.shields.io/badge/license-SSCL%20v2.0-blue)](LICENSE)

SchemaSmith is a state-based database schema management toolset for SQL Server. Define your desired database state as metadata — tables, views, procedures, triggers — and SchemaSmith transforms any target server to match.

Self-contained, single-file executables for Windows, Linux, and macOS. No .NET runtime install needed.

## Tools

- **SchemaQuench** — Deploys schema packages to databases. Modular deployment architecture with 9 execution slots, indexed view support, token system in all script folders, WhatIf analysis, and custom connection properties.
- **SchemaTongs** — Extracts database schemas into schema packages. Pure SQL extraction (no external dependencies), orphan detection, script validation, subfolder preservation.
- **DataTongs** — Extracts table data and generates MERGE scripts. Auto primary key detection, complex type support.

For the complete feature reference, see [docs/FEATURE_LIST.md](docs/FEATURE_LIST.md).

## Platform Support

| OS | x64 | ARM64 |
|----|-----|-------|
| Windows | win-x64 | win-arm64 |
| Linux | linux-x64 | linux-arm64 |
| macOS | osx-x64 | osx-arm64 |

## Installation

### GitHub Releases

Download self-contained ZIP packages from the [latest release](https://github.com/Schema-Smith/SchemaSmithyFree/releases/latest). Extract and run — no .NET runtime required.

### Build from Source

```bash
dotnet build SchemaSmithyFree.sln
```

For self-contained publishing:
```bash
# Windows
.\publish-tools.ps1

# Linux/macOS
./publish-tools.sh
```

## Quick Start

Build the tools and run the demo products against a local SQL Server:

```bash
docker compose -f demo/docker-compose.yml build
docker compose -f demo/docker-compose.yml up
```

This builds SchemaQuench from source, starts a SQL Server instance, and deploys the Northwind and AdventureWorks demo products. Connect to the server at `localhost:1440` with credentials from `demo/.env`.

## Running Tests

Integration tests require a SQL Server instance on `localhost,1440`. Start one via the demo docker-compose:

```bash
docker compose -f demo/docker-compose.yml up -d demoserver
```

Then run tests:

```bash
dotnet test SchemaSmithyFree.sln
```

## Demo Products

See [demo/README.md](demo/README.md) for AdventureWorks (71 tables), Northwind (13 tables), tutorials, and detailed docker-compose usage.

## License

SchemaSmith Community Edition is licensed under [SSCL v2.0](LICENSE). Use it freely to manage databases for your own products and services — SQL Server, PostgreSQL, or MySQL — with no restrictions on organization size, revenue, database size, or environment count. Not permitted: redistributing SchemaSmith as a standalone product, bundling it as a component of another product marketed to third parties, or offering it as a hosted or managed service. See the [LICENSE](LICENSE) for the full terms.

## Additional Resources

- [End-User Documentation](docs/end-user/README.md) — guide chapters and reference docs
- [Documentation Site](https://schemasmith.com/) — multi-platform documentation
