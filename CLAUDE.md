# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SchemaSmithyFree is the Community edition of SchemaSmith — free, SQL Server-only CLI tools for database schema management. It provides tools to extract ("cast") database schemas from existing databases and apply ("quench") schema definitions to target databases, enabling database-as-code workflows.

### Related Repositories

- **Community Command Center** (`C:\src\Community`) — Roadmap, feature matrices, tier decisions, development rules, plans. **Always start a session by reading the roadmap there:** `docs/plans/roadmap.md`
- **SchemaForge** (`C:\src\SchemaForge`) — Enterprise edition (paid, all platforms)

### Naming Convention

| Context | Name |
|---------|------|
| **User-facing** (docs, CLI help, error messages, env vars) | **SchemaSmith** |
| **Internal/development** (repo, code, namespaces) | **SchemaSmithyFree** |

## Build Commands

```bash
# Build entire solution
dotnet build SchemaSmithyFree.sln

# Run all unit tests
dotnet test Schema.UnitTests/Schema.UnitTests.csproj

# Run SchemaQuench unit tests
dotnet test SchemaQuench.UnitTests/SchemaQuench.UnitTests.csproj

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run integration tests (requires Docker SQL Server — see below)
dotnet test SchemaQuench.IntegrationTests/SchemaQuench.IntegrationTests.csproj
dotnet test SchemaTongs.IntegrationTests/SchemaTongs.IntegrationTests.csproj
dotnet test DataTongs.IntegrationTests/DataTongs.IntegrationTests.csproj

# Publish a tool as self-contained single file
dotnet publish SchemaQuench/SchemaQuench.csproj -r win-x64 -c Release
```

## Architecture

### Target Framework

All projects target `net10.0` (centralized in `Directory.Build.props`). Single-file self-contained deployment (no IL trimming). Published for 6 RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

`Directory.Build.props` sets `TargetFramework`, `TreatWarningsAsErrors=true`, and strips debug symbols in non-Debug builds. `global.json` pins the SDK version.

### Project Structure

- **Schema** — Core shared library. All tools reference this.
  - `Domain/` — Flat POCO classes: Product, Template, Table, Column, Index, ForeignKey, CheckConstraint, Statistic, FullTextIndex, XmlIndex, SqlServerVersion enum, SchemaPropertyAttribute
  - `Utility/` — ConfigHelper, SchemaGenerator, VersionHelper, CommandLineParser, RepositoryHelper, ResourceLoader, JsonHelper, ConfigurationLogger
  - `Isolators/` — FactoryContainer (simple IoC), wrapper classes for file system and environment (enables mocking)
  - `DataAccess/` — SQL connection helpers
  - `Scripts/` — Embedded SQL scripts (TableQuench, GenerateTableJson, ForgeKindler)
- **SchemaQuench** — Applies schema packages to databases. Reads Product.json, resolves templates, executes quench pipeline.
- **SchemaTongs** — Extracts database schemas into schema package format (JSON table definitions + SQL scripts).
- **DataTongs** — Extracts table data and generates MERGE scripts.
- **Schema.UnitTests** — Unit tests for Schema library
- **SchemaQuench.UnitTests** — Unit tests for SchemaQuench
- **\*.IntegrationTests** — Integration tests requiring Docker SQL Server

### Domain Model

Community uses flat POCO classes — no inheritance hierarchy, no platform abstraction (unlike Enterprise's DynamicBase pattern). `Table.Load(string)` takes a file path only.

Key types: `Product` (package metadata, script tokens, MinimumVersion), `Template` (database targeting, script folders, quench slots), `Table` (columns, indexes, FKs, constraints, statistics).

`SchemaPropertyAttribute` decorates domain properties with schema validation metadata (Required, Pattern, Min/Max). `SchemaGenerator` reflects over domain classes at runtime to produce JSON Schema files.

`SqlServerVersion` enum defines supported SQL Server versions (Sql2016–Sql2025) for MinimumVersion feature gating.

### Key Concepts

- **Schema packages** — Directory structure: Product.json + Templates/ with Template.json, table definitions (JSON), and SQL scripts organized by quench slot
- **Quench slots** — Execution order for SQL scripts: Before → Objects → AfterTablesObjects → TableData → After
- **Script tokens** — `{{TokenName}}` placeholders resolved from Product.json, Template.json, and config overrides
- **Config loading** — `{ToolName}.settings.json` → user secrets (DEBUG) → `SmithySettings_` env vars

## Testing

- **Framework:** NUnit with NSubstitute for mocking
- **Pattern:** `FactoryContainer` registers mock implementations; tests use `lock (FactoryContainer.SharedLockObject)` for isolation; `[SetUp]`/`[TearDown]` call `FactoryContainer.Clear()`
- **Integration tests** require a Docker SQL Server instance on port 1450. Start one with: `docker compose -f demo/docker-compose.yml up -d demoserver`
- **Zero warnings policy** — `TreatWarningsAsErrors` is enabled in `Directory.Build.props`

## Configuration

Each tool uses `{ToolName}.settings.json` (e.g., `SchemaQuench.settings.json`). Config is loaded by `ConfigHelper.GetAppSettingsAndUserSecrets()` with `AppContext.BaseDirectory` fallback. Environment variable prefix: `SmithySettings_` with `__` as hierarchy separator.

## Development Rules

All development rules, workflow processes, and Definition of Done are maintained in the Community command center repository. See `C:\src\Community\CLAUDE.md` for the authoritative set. Key rules: TDD, >85% coverage, plan discipline, feature branches on dev/v2, batch pushes (not after every commit — CI costs are significant), zero warnings.

## Copyright Headers

All new `.cs` files must start with:
```csharp
// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
```

All new `.sql` files in `Schema/Scripts/` must start with:
```sql
-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
```

Do not add headers to SQL files in `TestProducts/`, `demo/`, or `MigrationScripts/` — those are user-facing templates.
