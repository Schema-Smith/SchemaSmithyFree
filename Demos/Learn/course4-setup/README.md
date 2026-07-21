# Course 4 — Database Setup

These scripts create the nine cookbook databases on every sandbox engine (SQL Server, PostgreSQL,
MySQL, and MariaDB) — 36 databases in total. Each database is dedicated to one recipe so labs stay
hermetic and do not clash with one another.

## Prerequisite

The shared sandbox must be running. See [`Demos/Learn/README.md`](../README.md) for how to start it
and verify it is healthy before continuing.

## Run the setup

**macOS / Linux**

```bash
cd Demos/Learn/course4-setup
bash setup-databases.sh
```

**Windows (PowerShell)**

```powershell
cd Demos\Learn\course4-setup
.\setup-databases.ps1
```

Both scripts print `PASS` or `FAIL` for each of the 36 databases:

```
SQL Server
  cookbook_r1_prod           PASS
  cookbook_r1_nonprod        PASS
  cookbook_r2                PASS
  cookbook_r3                PASS
  cookbook_r4                PASS
  cookbook_r5                PASS
  cookbook_r6                PASS
  cookbook_r8                PASS
  cookbook_r9                PASS
PostgreSQL
  cookbook_r1_prod           PASS
  ...
MySQL
  cookbook_r1_prod           PASS
  ...
MariaDB
  cookbook_r1_prod           PASS
  ...

All 36 databases are ready.
```

## Databases created

| Recipe | Database(s) | Description |
| ------ | ----------- | ----------- |
| 1 — environment-aware | `cookbook_r1_prod`, `cookbook_r1_nonprod` | Two side-by-side targets to show prod vs. non-prod |
| 2 — policy enforces itself | `cookbook_r2` | Policy enforcement recipe |
| 3 — package asks the server | `cookbook_r3` | Server-interrogation recipe |
| 4 — assets travel | `cookbook_r4` | Asset-portability recipe |
| 5 — scripts write scripts | `cookbook_r5` | Script-generation recipe |
| 6 — surviving a rebuild | `cookbook_r6` | Rebuild-survival recipe |
| 8 — authoring recyclebin hooks | `cookbook_r8` | Custom drop/restore hook authoring recipe |
| 9 — Extensions as a source of truth | `cookbook_r9` | Data-dictionary-from-metadata recipe |

Each database is created on all four engines:

| Engine     | Databases |
| ---------- | --------- |
| SQL Server | `cookbook_r1_prod`, `cookbook_r1_nonprod`, `cookbook_r2`, `cookbook_r3`, `cookbook_r4`, `cookbook_r5`, `cookbook_r6`, `cookbook_r8`, `cookbook_r9` |
| PostgreSQL | `cookbook_r1_prod`, `cookbook_r1_nonprod`, `cookbook_r2`, `cookbook_r3`, `cookbook_r4`, `cookbook_r5`, `cookbook_r6`, `cookbook_r8`, `cookbook_r9` |
| MySQL      | `cookbook_r1_prod`, `cookbook_r1_nonprod`, `cookbook_r2`, `cookbook_r3`, `cookbook_r4`, `cookbook_r5`, `cookbook_r6`, `cookbook_r8`, `cookbook_r9` |
| MariaDB    | `cookbook_r1_prod`, `cookbook_r1_nonprod`, `cookbook_r2`, `cookbook_r3`, `cookbook_r4`, `cookbook_r5`, `cookbook_r6`, `cookbook_r8`, `cookbook_r9` |

## Connection details

These are throwaway sandbox credentials — **never reuse them anywhere real.**

| Engine     | Host        | Port    | User       | Password         |
| ---------- | ----------- | ------- | ---------- | ---------------- |
| SQL Server | `localhost` | `11433` | `sa`       | `Learn!Passw0rd` |
| PostgreSQL | `localhost` | `15432` | `postgres` | `Learn!Passw0rd` |
| MySQL      | `localhost` | `13306` | `root`     | `Learn!Passw0rd` |
| MariaDB    | `localhost` | `13307` | `root`     | `Learn!Passw0rd` |

## Re-running is safe

The scripts are idempotent — running them a second time makes no changes and still reports `PASS`
for every database. You can run them as many times as you like.
