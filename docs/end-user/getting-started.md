# Getting Started

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## Prerequisites

- **.NET 9.0 Runtime** — Required for the `net9.0` tools
- **.NET Framework 4.8.1** — Required for the `net481` tools (Windows only)
- **SQL Server** — Tested against SQL Server 2019 but should work for any version with a compatibility level of 130 or higher

---

## Installation

### ZIP Package

Download the ZIP package for the target framework (`net9.0` or `net481`). Extract to any directory. Each tool is a standalone executable:

- `SchemaQuench.exe` (or `SchemaQuench` on Linux/macOS)
- `SchemaTongs.exe`
- `DataTongs.exe`

Each tool includes a `{ToolName}.settings.json` file for configuration (e.g., `SchemaQuench.settings.json`).

### From Source

Clone the repository and build:

```bash
dotnet build SchemaSmithyFree.sln
```

Tools are built to their respective `bin/` directories.

---

## Configuration Loading

All tools load configuration from multiple sources in this order (later sources override earlier ones):

1. **`{ToolName}.settings.json`** — In the tool's directory, or specified via `--ConfigFile`
2. **User secrets** — Available in DEBUG builds only
3. **Environment variables** — Using the `SmithySettings_` prefix with `__` as hierarchy separator
4. **Command-line switches** — `--ConfigFile`, `--LogPath`

---

## Verification

Verify installation by checking the version:

```bash
SchemaQuench --version
SchemaTongs --version
DataTongs --version
```

---

## First Run: Extract and Apply

### 1. Extract a schema with SchemaTongs

Configure `SchemaTongs/SchemaTongs.settings.json` with the source database connection:

```json
{
    "Source": {
        "Server": "myserver",
        "Database": "MyDatabase"
    },
    "Product": {
        "Path": "C:\\SchemaPackages\\MyProduct",
        "Name": "MyProduct"
    },
    "Template": {
        "Name": "Main"
    }
}
```

Run SchemaTongs:

```bash
SchemaTongs
```

This creates a schema package at the specified path with all extractable database objects.

### 2. Apply with SchemaQuench

Configure `SchemaQuench/SchemaQuench.settings.json` with the target server and the schema package path:

```json
{
    "Target": {
        "Server": "myserver",
        "User": "",
        "Password": ""
    },
    "SchemaPackagePath": "C:\\SchemaPackages\\MyProduct"
}
```

Blank `User` and `Password` values use Windows authentication.

Run SchemaQuench:

```bash
SchemaQuench
```

SchemaQuench reads the schema package, identifies target databases via each template's database identification script, and transforms each database to match the defined state.

---

## Docker Quick Start

The repository includes a `docker-compose.yml` for testing. From the repository root:

```bash
docker compose build
docker compose up
```

This starts a SQL Server 2019 container with Full-Text Search and applies the included test product. Connect at `localhost` on the port defined in `.env` (default: 1440) with the credentials in `.env`.

---

## Related Documentation

- [Schema Packages](schema-packages.md)
- [Products and Templates](products-and-templates.md)
- [CLI Options](cli-options.md)
