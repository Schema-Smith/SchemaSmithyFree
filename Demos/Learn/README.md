# SchemaSmith Learn — lab sandbox

The hands-on labs for the [Learn SchemaSmith](https://learn.schemasmith.com) course run against a
throwaway four-engine database sandbox — SQL Server, PostgreSQL, MySQL, and MariaDB, all at once. Spin
it up, work through the labs, tear it down.

## Which lab comes next — the course map

The labs follow the [Learn SchemaSmith](https://learn.schemasmith.com) courses **in order**: work a
course's lessons on the site, run its lab folder here, then move to the next course. Each lab folder
maps to a course like this:

| Course | Lab folders | What it covers |
| ------ | ----------- | -------------- |
| **Course 1 · First deployment** | `module-01` … `module-04` | Install & connect, first package, WhatIf, extract an existing schema |
| **Course 2 · Going deeper** | `level2-module-01` … `level2-module-06` | Products, template fan-out, conditional deployment, script tokens, data delivery, custom metadata |
| **Course 3 · Ship it / operate it** | `course3-module-01` … `course3-module-04` | Team workflow, CI/CD gating, rollback |
| **Course 4 · Recipes** | `course4-recipe-01` … `course4-recipe-09` | A cookbook of task-focused recipes |
| **Course 5 · Switching tools** | `course5-module-01` … `course5-module-05` | Migrating from another migration tool |
| **Course 6 · Production & least privilege** | `course6-module-01` … `course6-module-06` | Operating in CI under least privilege |
| **Course 7 · Fleet fan-out** | `course7-module-01` … `course7-module-06` | One schema across a fleet of per-tenant databases |
| **Course 8 · Troubleshooting & recovery** | `course8-module-01` … `course8-module-06` | The when-a-deploy-fails playbook |
| **Course 9 · Polyglot shop** | `course9-module-01` … `course9-module-05` | All engines at once, each service independently deployable |

> **Heads-up on the folder names** — they use three conventions. Course 1's labs are the bare
> `module-*` folders; **Course 2's are the `level2-*` folders** (the site calls it "Course 2 · Going
> deeper" — the `level2` prefix is historical); Courses 3–9 are `courseN-*`. So the lab **after
> `module-04`** (the last Course 1 lab) is **`level2-module-01`**, then on through `course3-*` and up.

Courses 3–9 each include a `courseN-setup` folder — run it once before that course's modules.

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
