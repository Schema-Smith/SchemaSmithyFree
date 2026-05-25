# SchemaSmith Community Edition

*Terraform for SQL Server, Postgres, and MySQL databases*

> **🎉 SchemaSmith v2.0 is live.** State-based schema deployment for SQL Server, PostgreSQL, and MySQL — no migration scripts to author or order. Self-contained executables, no organization-size license caps. [Read the v2.0 announcement](https://github.com/Schema-Smith/SchemaSmith/discussions/226) · [Get started](https://schemasmith.com/walls-came-down.html?utm_source=github&utm_content=readme-banner&utm_campaign=20260513) · [Release notes](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v2.0.0)

![Build](https://github.com/Schema-Smith/SchemaSmith/actions/workflows/continuous-integration.yml/badge.svg)
[![Latest Release](https://img.shields.io/github/v/release/Schema-Smith/SchemaSmith)](https://github.com/Schema-Smith/SchemaSmith/releases/latest)
[![Chocolatey](https://img.shields.io/chocolatey/v/schemasmith)](https://community.chocolatey.org/packages/schemasmith)
[![License: SSCL v2.0](https://img.shields.io/badge/license-SSCL%20v2.0-blue)](LICENSE)

SchemaSmith is a state-based database schema management toolset for SQL Server, PostgreSQL, and MySQL. Define your desired database state as metadata — tables, views, procedures, indexes, constraints, data — and SchemaSmith transforms any target server to match. Same toolset, same package format, three engines — no migration scripts to author or order.

Self-contained, single-file executables for Windows, Linux, and macOS. No .NET runtime install needed.

## Tools

- **SchemaTongs** — Extracts databases into schema packages across all three platforms. Pure SQL extraction with no external SDKs, orphan detection with cleanup-script generation, post-extraction script validation, and subfolder preservation so your repository organization survives re-extraction.
- **SchemaQuench** — Deploys schema packages to SQL Server, PostgreSQL, and MySQL. 9 execution slots, conditional deployment via `ShouldApplyExpression`, secondary-server fan-out, FK-aware data delivery, checkpoint/resume, WhatIf analysis, indexed views (SQL Server), materialized views (PostgreSQL), and a token system that reaches every script.
- **DataTongs** — Extracts table data and generates platform-aware MERGE scripts for SQL Server, PostgreSQL, and MySQL. Auto primary-key detection, complex type support (geometry, hierarchyid, binary), and full token resolution including MySQL.

For the complete feature reference, see [docs/FEATURE_LIST.md](docs/FEATURE_LIST.md).

## Platform Support

| OS | x64 | ARM64 |
|----|-----|-------|
| Windows | win-x64 | win-arm64 |
| Linux | linux-x64 | linux-arm64 |
| macOS | osx-x64 | osx-arm64 |

## Installation

### Chocolatey (Windows)

```powershell
choco install schemasmith
```

Installs `schemaquench`, `schematongs`, and `datatongs` onto your PATH as a single combined package. Binaries are Authenticode-signed via Azure Trusted Signing — no SmartScreen warnings.

### GitHub Releases

Download self-contained ZIP packages from the [latest release](https://github.com/Schema-Smith/SchemaSmith/releases/latest). Extract and run — no .NET runtime required.

### Build from Source

```bash
dotnet build SchemaSmith.sln
```

For self-contained publishing of the CLI tools:
```bash
# Windows
.\build-schemaquench.cmd

# Linux/macOS
./build-schemaquench.sh
```

## Quick Start

Pick a platform and run the matching `run-demo` script:

```bash
# SQL Server
cd Demos/SqlServer && ./run-demo.sh

# PostgreSQL
cd Demos/PostgreSQL && ./run-demo.sh

# MySQL
cd Demos/MySQL && ./run-demo.sh
```

Each script builds SchemaQuench from source (if not already built), starts a containerized database server, and deploys the AdventureWorks, Chinook, Northwind, and Sakila demo products. Use `run-demo.cmd` on Windows. Connection details for each platform live in the `.env` file inside the platform folder.

## Running Tests

Integration tests run against all three platforms in parallel and expect database servers on these local ports:

| Platform   | Host      | Port |
|------------|-----------|------|
| SQL Server | 127.0.0.1 | 1440 |
| PostgreSQL | 127.0.0.1 | 5432 |
| MySQL      | 127.0.0.1 | 3306 |

The simplest way to bring all three up is to run the demo for each platform — the same containers serve as the integration-test backends:

```bash
cd Demos/SqlServer  && ./run-demo.sh
cd Demos/PostgreSQL && ./run-demo.sh
cd Demos/MySQL      && ./run-demo.sh
```

Then run tests:

```bash
dotnet test SchemaSmith.sln
```

Integration tests for a platform whose container isn't running will be skipped or fail — start only the platforms you need to exercise locally.

## Demo Products

Four sample databases ship across all three platforms:

- **AdventureWorks** (71 tables) — Microsoft's reference OLTP schema
- **Chinook** — digital media store, common across DB tutorials
- **Northwind** (13 tables) — classic small-business sample
- **Sakila** — DVD rental store, originally a MySQL reference

See [Demos/README.md](Demos/README.md) for the per-platform layout, credentials, and SQL Server tutorials.

## License

SchemaSmith Community Edition is licensed under [SSCL v2.0](LICENSE). Use it freely to manage databases for your own products and services — SQL Server, PostgreSQL, or MySQL — with no restrictions on organization size, revenue, database size, or environment count. Not permitted: redistributing SchemaSmith as a standalone product, bundling it as a component of another product marketed to third parties, or offering it as a hosted or managed service. See the [LICENSE](LICENSE) for the full terms.

## Additional Resources

- [End-User Documentation](docs/end-user/README.md) — guide chapters and reference docs
- [Documentation Site](https://schemasmith.com/) — multi-platform documentation
