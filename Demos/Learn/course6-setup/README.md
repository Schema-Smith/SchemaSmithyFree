# Course 6 — Database Setup

These scripts create and **seed** the Course 6 datafix databases on the sandbox engines — 9 databases
in total. Each tenant database carries the identical Shop schema (`Customer`, `Product`, `SalesOrder`,
`OrderItem`) and a **deterministic price-defect batch**: `OrderItem` rows on orders placed in May 2026
store `UnitPrice = ROUND(Product.UnitPrice * 0.81, 2)` — a 10% discount applied twice (the bug).
Orders from April and June 2026 carry the intended `ROUND(Product.UnitPrice * 0.90, 2)`.

The three tenant databases start equal so that Course 6 labs can canary the fix to `shop_tenant_a`
first before rolling it out to `shop_tenant_b` and `shop_tenant_c`.

## Prerequisite

The shared sandbox must be running. See [`Demos/Learn/README.md`](../README.md) for how to start it
and verify it is healthy before continuing.

## Run the setup

**macOS / Linux**

```bash
cd Demos/Learn/course6-setup
bash setup-databases.sh
```

**Windows (PowerShell)**

```powershell
cd Demos\Learn\course6-setup
.\setup-databases.ps1
```

Both scripts print `PASS` or `FAIL` for each database (`PASS` only after a shop table is confirmed):

```
SQL Server
  shop_tenant_a              PASS
  shop_tenant_b              PASS
  shop_tenant_c              PASS
PostgreSQL
  shop_tenant_a              PASS
  shop_tenant_b              PASS
  shop_tenant_c              PASS
MySQL
  shop_tenant_a              PASS
  shop_tenant_b              PASS
  shop_tenant_c              PASS

All 9 databases are seeded and ready (3 SQL Server, 3 PostgreSQL, 3 MySQL).
```

## Databases created

| Database        | Contains |
| --------------- | -------- |
| `shop_tenant_a` | Shop schema + price-defect batch (canary target in later labs) |
| `shop_tenant_b` | Shop schema + price-defect batch (identical to `a`) |
| `shop_tenant_c` | Shop schema + price-defect batch (identical to `a`) |

All three tenant databases are created on all three engines (SQL Server, PostgreSQL, MySQL).

## Connection details

These are throwaway sandbox credentials — **never reuse them anywhere real.**

| Engine     | Host        | Port    | User       | Password         |
| ---------- | ----------- | ------- | ---------- | ---------------- |
| SQL Server | `localhost` | `11433` | `sa`       | `Learn!Passw0rd` |
| PostgreSQL | `localhost` | `15432` | `postgres` | `Learn!Passw0rd` |
| MySQL      | `localhost` | `13306` | `root`     | `Learn!Passw0rd` |

## Verifying the bad batch

After running the setup, confirm the defect is present and equal across all three tenants.

**SQL Server** — run against each of `shop_tenant_a`, `shop_tenant_b`, `shop_tenant_c`:

```sql
SELECT COUNT(*) AS bad_batch_rows
FROM   dbo.OrderItem  oi
JOIN   dbo.SalesOrder so ON so.OrderId = oi.OrderId
WHERE  so.OrderDate >= '2026-05-01'
  AND  so.OrderDate  < '2026-06-01';
```

**PostgreSQL** — run against each tenant database:

```sql
SELECT COUNT(*) AS bad_batch_rows
FROM   orderitem  oi
JOIN   salesorder so ON so.orderid = oi.orderid
WHERE  so.orderdate >= '2026-05-01'
  AND  so.orderdate  < '2026-06-01';
```

**MySQL** — run against each tenant database:

```sql
SELECT COUNT(*) AS bad_batch_rows
FROM   `OrderItem`  oi
JOIN   `SalesOrder` so ON so.OrderId = oi.OrderId
WHERE  so.OrderDate >= '2026-05-01'
  AND  so.OrderDate  < '2026-06-01';
```

Expected result: **10 rows** in every tenant on every engine (5 May-2026 orders × 2 items each,
with orders 105–109 carrying 10 affected `OrderItem` rows in total).

## Re-running is safe

The seed scripts are idempotent — every table is dropped and recreated, so a second run restores the
exact starting state and still reports `PASS` for every database. Run them as often as you like; if a
lab leaves a database in an odd state, re-run the setup to reset it.
