# SchemaSmith Learn — lab sandbox

The hands-on labs for the [Learn SchemaSmith](https://learn.schemasmith.com) course run against a
throwaway three-engine database sandbox — SQL Server, PostgreSQL, and MySQL, all at once. Spin it
up, work through the labs, tear it down.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) (Desktop or Engine) with Compose v2.

## Start the sandbox

```bash
cd docker
docker compose up -d
```

The first run pulls three database images — give it a few minutes.

## Verify it's ready

```bash
./verify-sandbox.sh        # macOS / Linux
.\verify-sandbox.ps1       # Windows PowerShell
```

Each engine reports `PASS` once it has finished warming up. If one says it isn't healthy yet, wait a
minute and run it again — SQL Server in particular takes a little while on first boot.

## Connection details (throwaway — sandbox only)

| Engine     | Host        | Port    | User       | Password         | Database       |
| ---------- | ----------- | ------- | ---------- | ---------------- | -------------- |
| SQL Server | `localhost` | `11433` | `sa`       | `Learn!Passw0rd` | created in labs |
| PostgreSQL | `localhost` | `15432` | `postgres` | `Learn!Passw0rd` | `learn`        |
| MySQL      | `localhost` | `13306` | `root`     | `Learn!Passw0rd` | `learn`        |

These credentials are intentionally simple and public. The sandbox is disposable — **never reuse
them anywhere real.** The ports are offset (`11433` / `15432` / `13306`) so the sandbox won't collide
with a default SQL Server, PostgreSQL, or MySQL you may already be running locally.

## Tear it down

```bash
cd docker
docker compose down -v     # -v also removes the data volumes
```

## How this relates to the other demos

The per-engine demos under `Demos/SqlServer`, `Demos/PostgreSQL`, and `Demos/MySQL` stand up a full
demo: they build the CLI and auto-deploy the sample databases. This sandbox is deliberately lighter
— it stands up empty engines so you install the CLI yourself (Module 1) and run it by hand, the way
you would against your own database. It reuses the same engine versions and the MySQL
function-trust flag those demos rely on, with throwaway credentials and offset ports.
