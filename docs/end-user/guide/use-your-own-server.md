# Use Your Own Server (No Docker)

Already run SQL Server, PostgreSQL, MySQL, or MariaDB? You don't need Docker to try SchemaSmith. Point one helper script at any reachable instance and it stands up the entire demo — the same databases the Docker demo builds, minus the containers. It's the database-level equivalent of `docker compose up`, for people who already have infrastructure.

Every supported engine ships the same helper, side by side with its Docker launcher:

| Engine | Helper folder | Client needed |
| --- | --- | --- |
| SQL Server | `Demos/SqlServer/` | `sqlcmd` |
| PostgreSQL | `Demos/PostgreSQL/` | `psql` |
| MySQL | `Demos/MySQL/` | `mysql` |
| MariaDB | `Demos/MariaDb/` | `mariadb` |

## Two ways to run the demo

- **Docker** — throwaway, zero setup. `run-demo` spins up a disposable server, deploys the demo, and you tear it all down when you're done. Reach for this when you *don't* already have a server. See [Quick Start](02-quick-start.md).
- **Your own server** — this page. A local instance, a shared QA box, a container you manage — the `deploy-to-endpoint` helper resets and deploys the demo databases directly onto it, no Docker involved.

Either path lands you in the same place: the AdventureWorks, Northwind, Sakila, and Chinook demo databases (SQL Server and PostgreSQL add TenantCRM; SQL Server also adds the shared `TestSecondary` scaffolding), all built on the shared `TestMain` control database, ready to explore.

## Prerequisite: the engine's command-line client

The helper uses your engine's command-line client to create, stamp, and drop the demo databases, so that client must be on your `PATH`. None are bundled — install the one for your engine and re-open your shell.

> **SQL Server — `sqlcmd`.** `winget install Microsoft.SQLServer.SqlCmd` (Windows) · `brew install sqlcmd` (macOS) · `mssql-tools18` per the [Microsoft docs](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility) (Linux). Verify with `sqlcmd -?`.

> **PostgreSQL — `psql`.** `winget install PostgreSQL.PostgreSQL` (Windows) · `brew install libpq` or `postgresql` (macOS) · `apt-get install postgresql-client` / `dnf install postgresql` (Linux). Verify with `psql --version`.

> **MySQL — `mysql`.** `winget install Oracle.MySQL` (Windows) · `brew install mysql-client` (macOS) · `apt-get install mysql-client` / `dnf install mysql` (Linux). Verify with `mysql --version`.

> **MariaDB — `mariadb`.** `winget install MariaDB.Client` (Windows) · `brew install mariadb` (macOS) · `apt-get install mariadb-client` / `dnf install MariaDB-client` (Linux). Verify with `mariadb --version`. (`mysql` is a legacy symlink being phased out — the helper calls `mariadb`.)

Each helper checks for its client and stops with these same instructions if it can't find it.

## Provision the demo

Give the helper your server and a login that can create and drop databases. SQL Server takes the port comma-joined to the server; the other engines take a separate `--port` (shown with each engine's default).

**SQL Server (PowerShell / bash):**

```powershell
cd Demos\SqlServer
.\deploy-to-endpoint.ps1 -Server 'localhost,1433' -User sa -Password 'YourPassword'
```

```bash
cd Demos/SqlServer
./deploy-to-endpoint.sh --server your-server --user sa --password 'YourPassword'
```

> **PowerShell quoting.** Quote the server: `-Server 'localhost,1433'`. Unquoted, PowerShell reads the comma as a list separator and `host,port` becomes two values instead of one — the quotes keep it a single string.

**PostgreSQL (PowerShell / bash):**

```powershell
cd Demos\PostgreSQL
.\deploy-to-endpoint.ps1 -Server localhost -Port 5432 -User postgres -Password 'YourPassword'
```

```bash
cd Demos/PostgreSQL
./deploy-to-endpoint.sh --server localhost --port 5432 --user postgres --password 'YourPassword'
```

**MySQL / MariaDB (PowerShell / bash):**

```powershell
cd Demos\MySQL          # or Demos\MariaDb
.\deploy-to-endpoint.ps1 -Server localhost -Port 3306 -User root -Password 'YourPassword'
```

```bash
cd Demos/MySQL          # or Demos/MariaDb
./deploy-to-endpoint.sh --server localhost --port 3306 --user root --password 'YourPassword'
```

> **Windows Authentication (SQL Server only).** Omit the user and password — `-Server 'localhost,1433'` on its own (PowerShell) or `--server your-server` on its own (bash) — and the helper connects as your current Windows identity: SchemaQuench builds an `Integrated Security` connection and `sqlcmd` uses `-E`. PostgreSQL, MySQL, and MariaDB always need credentials.

On every run the helper does three things:

1. **Resets** — drops the demo databases it previously created (and *only* those — see below), so you always start from a known-clean state.
2. **Bootstraps** — creates the shared `TestMain` control database (and `TestSecondary` on SQL Server).
3. **Deploys** — runs the shipped SchemaQuench against each demo package, exactly as the Docker demo does.

Before dropping anything, it prints the exact list of databases it will replace and waits for you to type `yes`. Pass `-Force` (PowerShell) or `--force` (bash) to skip the prompt in automation.

> **Note:** the helper reuses the shipped SchemaQuench and the demo packages unchanged — it adds no new tool and alters nothing about how deployment works. It's only the orchestration the Docker demo gives you for free, unbundled from Docker.

## If a name collides

The demo uses friendly names like `Northwind` and `AdventureWorks` — and you might already have a real `Northwind` on that server. The helper will not touch it.

When it creates a database, it stamps it with an ownership marker, and it only ever drops databases carrying that stamp. If a demo name already exists *without* the stamp — your real database — the helper stops and refuses, naming the collision:

```
These databases already exist on your-server but were NOT created by this helper:
  Northwind
```

> **Where the stamp lives.** The marker is per engine, but the safety rule is identical everywhere: SQL Server uses a `SchemaSmith_DemoProvisioned` extended property, PostgreSQL a database comment (`COMMENT ON DATABASE`), and MySQL / MariaDB a small `SchemaSmith_DemoProvisioned` marker table inside each provisioned database.

To proceed, rename the demo's copy so it no longer collides. Open the engine's `demo-databases.manifest` and change the `NAME` column for that row — for example `Northwind` → `SS_Northwind`:

```
product|SS_Northwind|NorthwindDb|Northwind
```

Re-run the helper. It now deploys the same package to a database named `SS_Northwind`, and your real `Northwind` is never touched. Only the `NAME` column changes — leave `TOKEN` and `PACKAGE` as they are.

> **Full-Text Search is optional (SQL Server).** The AdventureWorks demo includes a full-text catalog, three full-text indexes, and a search procedure. If your server doesn't have Full-Text Search installed, those objects are skipped automatically — you'll see `Skipping folder ... ShouldApplyExpression evaluated false` in the log, telling you exactly what was left out and why — and the rest of AdventureWorks deploys normally. Install Full-Text Search on the server if you want the full-text objects.

## Bring your own database instead

Want to point SchemaSmith at a database you *already* have, rather than the demo set? That's SchemaTongs' job — it grips your live schema and casts it into a version-controlled package. No helper needed; it's a connection-settings change. Walk through it in [Quick Start → Cast with SchemaTongs](02-quick-start.md#step-2-cast-with-schematongs).
