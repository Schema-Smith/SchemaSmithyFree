# Course 9 — Setup: stand up the three services

Northwind Commerce runs three services, each on its own engine and each managed
by its own **native** SchemaSmith package:

| Service | Engine | Database |
| --- | --- | --- |
| Orders | SQL Server | `orders` |
| Catalog | PostgreSQL | `catalog` |
| Sessions / Events | MySQL | `sessions` |

This lab creates the three empty service databases the rest of Course 9 deploys
into. The packages are native and separate — an Orders package is a SQL Server
package, a Catalog package is a PostgreSQL package, a Sessions package is a MySQL
package. Each service is independently deployable; there is no single package that
spans engines and no combined deploy step. The consistency across the three
services is in the tooling and the workflow, not in a shared artifact. In
production each service would live in its own repository; they sit together here
only because that is how lab bundles ship.

## Prerequisites

- The three-engine sandbox is up (`Demos/Learn/docker`) — see [`../README.md`](../README.md).
- `schemaquench --version` answers **2.3.0** or later on your PATH. New to the
  CLI? Install it in [Course 1, Module 1](https://learn.schemasmith.com/01-install-connect/).

## Step 1 — create the service databases

**macOS / Linux**

```bash
cd Demos/Learn/course9-setup
bash setup-databases.sh
```

**Windows (PowerShell)**

```powershell
cd Demos\Learn\course9-setup
.\setup-databases.ps1
```

Prints `PASS` per engine once its service database exists. Re-running is safe —
every `CREATE` is guarded.

## What the script does

| Engine | Database created | Guard |
| --- | --- | --- |
| SQL Server | `orders` | `IF DB_ID('orders') IS NULL` |
| PostgreSQL | `catalog` | `SELECT … WHERE NOT EXISTS …`, run via psql `\gexec` |
| MySQL | `sessions` | `CREATE DATABASE IF NOT EXISTS` |

No schema is deployed here. Module 1 deploys the first native package into each
of these databases.

Next: **Module 1 — Same change, three engines**, where you apply one identical
schema evolution to all three services — as three independent native packages —
and feel the workflow stay the same even as the DDL goes native per engine.
