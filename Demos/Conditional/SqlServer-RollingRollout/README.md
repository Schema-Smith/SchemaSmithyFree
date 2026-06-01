# SQL Server Rolling-Rollout Demo

One schema package. Three tenant databases. Heavy DDL rolled out one tenant per maintenance window — gated by a per-database `RolloutControl` row the DBA flips when each tenant is ready.

## What's in here

A small SchemaSmith schema package targeting three tenant databases (`Tenant_A`, `Tenant_B`, `Tenant_C`) on a single SQL Server 2022 instance. The package declares an `OrderHistory` table and a **nonclustered columnstore index** on it for analytical queries. The NCCI is wrapped in a `ShouldApplyExpression` that checks a per-database `RolloutControl` table:

```json
{
  "Name": "NCCI_OrderHistory_Analytics",
  "ColumnStore": true,
  "Clustered": false,
  "IncludeColumns": "TenantId, CustomerId, OrderDate, OrderTotal",
  "CompressionType": "COLUMNSTORE",
  "ShouldApplyExpression": "EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'OrderHistoryColumnstore' AND status = 'Ready')"
}
```

The demo bootstraps the three tenants with this initial state:

| Database | `RolloutControl.status` |
|---|---|
| `Tenant_A` | `Ready` |
| `Tenant_B` | `Pending` |
| `Tenant_C` | `Pending` |

## Running it

```bash
./run-demo.sh        # macOS / Linux
run-demo.cmd         # Windows
```

The launcher publishes SchemaQuench, brings up `docker compose`, and runs the deploy against all three tenant databases. A final verification service prints what landed in each.

Expected output (the bottom half):

```
--- Tenant_A ---
 feature                   status
 OrderHistoryColumnstore   Ready
 index_name                       type_desc
 PK_OrderHistory                  CLUSTERED
 NCCI_OrderHistory_Analytics      NONCLUSTERED COLUMNSTORE

--- Tenant_B ---
 feature                   status
 OrderHistoryColumnstore   Pending
 index_name                       type_desc
 PK_OrderHistory                  CLUSTERED

--- Tenant_C ---
 feature                   status
 OrderHistoryColumnstore   Pending
 index_name                       type_desc
 PK_OrderHistory                  CLUSTERED
```

Same package. Three databases. One rollout pass. Only `Tenant_A` (the one approved for this window) picked up the heavy DDL.

## Rolling to the next tenant

To advance `Tenant_B` in a later maintenance window, flip its rollout row and re-run the quench:

```bash
docker compose exec demoserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d Tenant_B -Q \
  "UPDATE dbo.RolloutControl SET status = 'Ready' WHERE feature = 'OrderHistoryColumnstore';"

docker compose run --rm quench
docker compose run --rm verify
```

After the second pass:
- `Tenant_B` picks up the NCCI.
- `Tenant_A` is **left alone** — the index already matches the declared shape, so SchemaSmith's state-based engine doesn't re-create it.
- `Tenant_C` continues to wait.

That's the rollout discipline: one tenant per window, the same package every time, no per-tenant migration files growing in your repo.

## Why this matters

Multi-tenant SQL Server footprints (eighty-seven tenant databases on one instance — same name, same shape, multiplied by tenant count) hit a problem unique to scale: a single heavy DDL operation that takes hours per table can't be deployed everywhere in one maintenance window. The conventional fix is a hand-maintained tenant list and a wrapper script that reads it; the rollout-state tracking lives in a spreadsheet or a wiki page or a comment block in the deploy runbook.

With SchemaSmith's `ShouldApplyExpression`, the rollout state lives **in the database itself**, evaluated by SQL Server at deploy time. The DBA flips a row, the deployment picks up the change. The schema package doesn't grow by one file per tenant. The CI script doesn't grow at all.

The "already-indexed" guardrail you might expect to write next to that — `AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'NCCI_OrderHistory_Analytics' AND type_desc = 'NONCLUSTERED COLUMNSTORE')` — is **implicit** in SchemaSmith's state-based engine. Once the index matches the declared shape, no re-build fires; you don't need to write that predicate yourself.

## Cleanup

```bash
docker compose down --volumes
```

## Related

- Article: *The Production Server That Can't Be Upgraded — and the Deployment Pipeline That Has to Live With It* (LinkedIn, 2026-06-11)
- Sister demos:
  - [`../PostgreSQL-VersionGate`](../PostgreSQL-VersionGate) — version-gating a virtual generated column on PG18+
  - [`../MySQL-VersionGate`](../MySQL-VersionGate) — version-gating a `VECTOR` column on MySQL 9+
