# SchemaSmith Learn — lab sandbox

The hands-on labs for the [Learn SchemaSmith](https://learn.schemasmith.com) course run against a
throwaway four-engine database sandbox — SQL Server, PostgreSQL, MySQL, and MariaDB, all at once. Spin
it up, work through the labs, tear it down.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) (Desktop or Engine) with Compose v2 — for the throwaway sandbox. Already run one of these engines? Skip Docker entirely: see [Use your own server](#use-your-own-server-instead-no-docker) below.

## Start the sandbox

```bash
cd docker
docker compose up -d
```

The first run pulls four database images — give it a few minutes.

## Verify it's ready

```bash
./verify-sandbox.sh        # macOS / Linux
.\verify-sandbox.ps1       # Windows PowerShell
```

Each engine reports `PASS` once it has finished warming up. If one says it isn't healthy yet, wait a
minute and run it again — SQL Server in particular takes a little while on first boot.

## Connection details (throwaway — sandbox only)

| Engine     | Host        | Port    | User       | Password         | Database |
| ---------- | ----------- | ------- | ---------- | ---------------- | -------- |
| SQL Server | `localhost` | `11433` | `sa`       | `Learn!Passw0rd` | `learn`  |
| PostgreSQL | `localhost` | `15432` | `postgres` | `Learn!Passw0rd` | `learn`  |
| MySQL      | `localhost` | `13306` | `root`     | `Learn!Passw0rd` | `learn`  |
| MariaDB    | `localhost` | `13307` | `root`     | `Learn!Passw0rd` | `learn`  |

All four engines come up with a `learn` database ready to go. PostgreSQL, MySQL, and MariaDB create it
from environment variables; SQL Server's image can't, so a one-shot `sqlserver-init` service creates it
once the engine is healthy, then exits.

These credentials are intentionally simple and public. The sandbox is disposable — **never reuse
them anywhere real.** The ports are offset (`11433` / `15432` / `13306`) so the sandbox won't collide
with a default SQL Server, PostgreSQL, or MySQL you may already be running locally.

## Tear it down

```bash
cd docker
docker compose down -v     # -v also removes the data volumes
```

## Use your own server instead (no Docker)

Already run SQL Server, PostgreSQL, MySQL, or MariaDB? You don't need Docker for the labs. From this
directory, point the helper at your server and it creates — or cleanly resets — the empty `learn`
database, the no-Docker equivalent of the sandbox above:

```bash
./deploy-to-endpoint.sh --engine postgres --server your-host --port 5432 --user you --password '…'
#   --engine:  sqlserver | postgres | mysql | mariadb
#   Windows :  .\deploy-to-endpoint.ps1 -Engine … -Server … -Port … -User … -Password …
```

It needs that engine's command-line client on your `PATH` (`sqlcmd` / `psql` / `mysql` / `mariadb`).
SQL Server also accepts Windows Authentication — omit `--user`/`--password`. The helper stamps the
database it creates and only ever drops a stamped `learn`; an existing `learn` it didn't create is
refused, never touched. Then set each lab's `connect.settings.json` to your host, port, and
credentials instead of the sandbox values. Full walkthrough:
[Use your own server](https://github.com/Schema-Smith/SchemaSmith/blob/main/docs/end-user/guide/use-your-own-server.md).

## How this relates to the other demos

The per-engine demos under `Demos/SqlServer`, `Demos/PostgreSQL`, and `Demos/MySQL` stand up a full
demo: they build the CLI and auto-deploy the sample databases. This sandbox is deliberately lighter
— it stands up empty engines so you install the CLI yourself (Module 1) and run it by hand, the way
you would against your own database. It reuses the same engine versions and the MySQL
function-trust flag those demos rely on, with throwaway credentials and offset ports.
