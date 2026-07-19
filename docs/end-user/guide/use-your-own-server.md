# Use Your Own Server (No Docker)

Already run SQL Server? You don't need Docker to try SchemaSmith. Point one helper script at any reachable instance and it stands up the entire demo — the same databases the Docker demo builds, minus the containers. It's the database-level equivalent of `docker compose up`, for people who already have infrastructure.

## Two ways to run the demo

- **Docker** — throwaway, zero setup. `run-demo` spins up a disposable SQL Server, deploys the demo, and you tear it all down when you're done. Reach for this when you *don't* already have a server. See [Quick Start](02-quick-start.md).
- **Your own server** — this page. A local instance, a shared QA box, a container you manage — the `deploy-to-endpoint` helper resets and deploys the demo databases directly onto it, no Docker involved.

Either path lands you in the same place: the AdventureWorks, Northwind, Sakila, Chinook, and TenantCRM demo databases (plus the shared `TestMain` / `TestSecondary` scaffolding), ready to explore.

> **Prerequisite: `sqlcmd`.** The helper uses the SQL Server command-line client to create and stamp the demo databases, so `sqlcmd` must be on your `PATH`. It isn't bundled — install it and re-open your shell:
>
> - **Windows:** `winget install Microsoft.SQLServer.SqlCmd`
> - **macOS:** `brew install sqlcmd`
> - **Linux:** install `mssql-tools18` per the [Microsoft docs](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility).
>
> Confirm it's ready with `sqlcmd -?` — it should print usage. The helper checks for it and stops with these same instructions if it can't find it.

## Provision the demo

The helper lives beside the Docker launcher in `Demos/SqlServer/`. Give it your server and a login that can create and drop databases:

**Windows (PowerShell):**

```powershell
cd Demos\SqlServer
.\deploy-to-endpoint.ps1 -Server 'localhost,1433' -User sa -Password 'YourPassword'
```

> **PowerShell quoting.** Quote the server: `-Server 'localhost,1433'`. Unquoted, PowerShell reads the comma as a list separator and `host,port` becomes two values instead of one — the quotes keep it a single string.

**macOS / Linux (bash):**

```bash
cd Demos/SqlServer
./deploy-to-endpoint.sh --server your-server --user sa --password 'YourPassword'
```

On every run the helper does three things:

1. **Resets** — drops the demo databases it previously created (and *only* those — see below), so you always start from a known-clean state.
2. **Bootstraps** — creates the shared `TestMain` / `TestSecondary` databases.
3. **Deploys** — runs the shipped SchemaQuench against each demo package, exactly as the Docker demo does.

Before dropping anything, it prints the exact list of databases it will replace and waits for you to type `yes`. Pass `-Force` (PowerShell) or `--force` (bash) to skip the prompt in automation.

> **Note:** the helper reuses the shipped SchemaQuench and the demo packages unchanged — it adds no new tool and alters nothing about how deployment works. It's only the orchestration the Docker demo gives you for free, unbundled from Docker.

## If a name collides

The demo uses friendly names like `Northwind` and `AdventureWorks` — and you might already have a real `Northwind` on that server. The helper will not touch it.

When it creates a database, it stamps it with an ownership marker (a `SchemaSmith_DemoProvisioned` extended property), and it only ever drops databases carrying that stamp. If a demo name already exists *without* the stamp — your real database — the helper stops and refuses, naming the collision:

```
These databases already exist on your-server but were NOT created by this helper:
  Northwind
```

To proceed, rename the demo's copy so it no longer collides. Open `Demos/SqlServer/demo-databases.manifest` and change the `NAME` column for that row — for example `Northwind` → `SS_Northwind`:

```
product|SS_Northwind|NorthwindDb|Northwind
```

Re-run the helper. It now deploys the same package to a database named `SS_Northwind`, and your real `Northwind` is never touched. Only the `NAME` column changes — leave `TOKEN` and `PACKAGE` as they are.

> **Full-Text Search is optional.** The AdventureWorks demo includes a full-text catalog, three full-text indexes, and a search procedure. If your server doesn't have Full-Text Search installed, those objects are skipped automatically — you'll see `Skipping folder ... ShouldApplyExpression evaluated false` in the log, telling you exactly what was left out and why — and the rest of AdventureWorks deploys normally. Install Full-Text Search on the server if you want the full-text objects.

## Bring your own database instead

Want to point SchemaSmith at a database you *already* have, rather than the demo set? That's SchemaTongs' job — it grips your live schema and casts it into a version-controlled package. No helper needed; it's a connection-settings change. Walk through it in [Quick Start → Cast with SchemaTongs](02-quick-start.md#step-2-cast-with-schematongs).
