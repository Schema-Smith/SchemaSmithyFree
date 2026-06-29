# Course 5, Module 1 — Migrating from Flyway (lab)

Goal: take a database that **Flyway** built — shop schema plus `flyway_schema_history` — and move it
to SchemaSmith with **extract-and-go**. You'll cast the live database to declarative files, leave the
Flyway ledger behind, and quench to a clean no-op that proves the cast is faithful. All three engines.

You do **not** run Flyway. The `before/` folder shows a real Flyway project for reference; the setup
already applied its end state to `shop_from_flyway` on each engine.

## Before you start

- The [sandbox](../docker) is up (`docker compose up -d`) and verified (all three engines `PASS`).
- The Course 5 databases exist — run [`../course5-setup`](../course5-setup) once (creates and seeds
  `shop_from_flyway`, among others).
- The CLI is on your PATH (`schematongs --version` and `schemaquench --version` answer). New to the
  CLI? Course 1, Module 1 walks the install.

Each engine folder (`sqlserver/`, `postgres/`, `mysql/`) ships a `SchemaTongs.settings.json` (the
extract config), a `quench.settings.json` (the deploy config), and the `Package/` this lab produced —
so you can diff your own extract against it.

## Step 1: Look at what Flyway left behind

```bash
# SQL Server
docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d shop_from_flyway -Q \"SELECT name FROM sys.tables ORDER BY name\""
# → Customer, OrderItem, Product, SalesOrder, flyway_schema_history
```

Four shop tables and Flyway's history table. That history table is the thing you're walking away from.

## Step 2: Extract — name the tables you keep

Open `sqlserver/SchemaTongs.settings.json`. The whitelist is the whole trick:

```json
"ShouldCast": { "ObjectList": "dbo.Customer,dbo.Product,dbo.SalesOrder,dbo.OrderItem" }
```

```bash
cd sqlserver
schematongs --ConfigFile:SchemaTongs.settings.json
ls Package/Templates/Main/Tables/
```

```
=== Casting Summary ===
  Tables:     4 extracted, 0 errors

dbo.Customer.json  dbo.OrderItem.json  dbo.Product.json  dbo.SalesOrder.json
```

Four tables. No `flyway_schema_history.json` — you named the tables you wanted, and the ledger wasn't
on the list. You didn't delete it; you just didn't invite it.

## Step 3: Quench — adopt, then prove the no-op

```bash
schemaquench --ConfigFile:quench.settings.json
```

The first run adopts your existing tables (stamps them as managed, stands up SchemaSmith's own
bookkeeping in a separate `SchemaSmith` schema). Run it a second time and nothing happens — a clean
no-op. That no-op is the proof: the package is a faithful cast of the live database.

```bash
# confirm flyway_schema_history is still there, untouched — drop it whenever you like
docker exec learn-sqlserver bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d shop_from_flyway -Q \"SELECT name FROM sys.tables WHERE name='flyway_schema_history'\""
# → flyway_schema_history  (left exactly where it was)
```

## Step 4: Do it on PostgreSQL and MySQL

Same three steps in `postgres/` and `mysql/`. Only the whitelist's dialect differs:

| | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| `ObjectList` | `dbo.Customer,…` | `public.customer,…` | `Customer,…` |

Each one extracts four tables, leaves `flyway_schema_history` behind, and quenches to a clean no-op.

## The principle

Flyway's model is the history table — the ordered ledger of every change. SchemaSmith doesn't keep one,
because it doesn't replay your steps; it reads your current shape and converges to it. So you don't port
a hundred migrations. You extract the state they already produced, leave the ledger cold, and manage
forward from one declared source.
