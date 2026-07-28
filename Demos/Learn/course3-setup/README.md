# Course 3 — Environment Setup

These scripts create the `ordersservice_dev`, `ordersservice_staging`, and `ordersservice_prod`
databases on every sandbox engine (SQL Server, PostgreSQL, MySQL, MariaDB) — twelve databases in
total. They support the multi-environment promotion labs in Course 3.

## Prerequisite

The shared sandbox must be running. See [`Demos/Learn/README.md`](../README.md) for how to start it
and verify it is healthy before continuing.

## Run the setup

**macOS / Linux**

```bash
cd Demos/Learn/course3-setup
bash setup-environments.sh
```

**Windows (PowerShell)**

```powershell
cd Demos\Learn\course3-setup
.\setup-environments.ps1
```

Both scripts print `PASS` or `FAIL` for each of the twelve databases:

```
SQL Server
  ordersservice_dev          PASS
  ordersservice_staging      PASS
  ordersservice_prod         PASS
PostgreSQL
  ordersservice_dev          PASS
  ordersservice_staging      PASS
  ordersservice_prod         PASS
MySQL
  ordersservice_dev          PASS
  ordersservice_staging      PASS
  ordersservice_prod         PASS
MariaDB
  ordersservice_dev          PASS
  ordersservice_staging      PASS
  ordersservice_prod         PASS

All twelve databases are ready.
```

## Databases created

| Engine     | Databases                                                          |
| ---------- | ------------------------------------------------------------------ |
| SQL Server | `ordersservice_dev`, `ordersservice_staging`, `ordersservice_prod` |
| PostgreSQL | `ordersservice_dev`, `ordersservice_staging`, `ordersservice_prod` |
| MySQL      | `ordersservice_dev`, `ordersservice_staging`, `ordersservice_prod` |
| MariaDB    | `ordersservice_dev`, `ordersservice_staging`, `ordersservice_prod` |

## Connection details

These are the same throwaway sandbox credentials as the main sandbox — **never reuse them anywhere
real.**

| Engine     | Host        | Port    | User       | Password         |
| ---------- | ----------- | ------- | ---------- | ---------------- |
| SQL Server | `localhost` | `11433` | `sa`       | `Learn!Passw0rd` |
| PostgreSQL | `localhost` | `15432` | `postgres` | `Learn!Passw0rd` |
| MySQL      | `localhost` | `13306` | `root`     | `Learn!Passw0rd` |
| MariaDB    | `localhost` | `13307` | `root`     | `Learn!Passw0rd` |

## Re-running is safe

The scripts are idempotent — running them a second time makes no changes and still reports `PASS`
for every database. You can run them as many times as you like.

## Starting over: `--reset`

Modules share these databases, and each one deploys its own package into them. That's the point —
it's how the course shows a single product moving through environments. But **Module 5's capstone
also installs infrastructure the other modules know nothing about**: a `recyclebin` schema, a
registry table, and custom drop/restore procedures. Go back to an earlier module afterwards and its
package meets objects it never declared.

When that happens — or any time you want a clean slate — reset the databases:

```bash
bash setup-environments.sh --reset
```

```powershell
.\setup-environments.ps1 -Reset
```

Each database is dropped and recreated empty, reported as `PASS (reset)`. **Only databases these
scripts created are ever dropped.** On your own server, a database carrying one of these names that
the labs didn't create is refused and left untouched — you'll be told to rename or move it. Nothing
of yours is at risk.

Re-run the module you're returning to from its first step; everything it needs, it creates.
