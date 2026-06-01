# Conditional Deployment Demos

Three runnable demos showing `ShouldApplyExpression` on real engines. One schema package per demo, two or three engine targets per demo, observable difference in the deployed shape — all driven by the SQL expression in the schema package, not by branching the CI script.

| Demo | Engine pair | Gated component | Condition |
|---|---|---|---|
| [PostgreSQL Version-Gate](PostgreSQL-VersionGate) | PG15 ↔ PG18 | Virtual generated column | `current_setting('server_version_num')::int >= 180000` |
| [SQL Server Rolling-Rollout](SqlServer-RollingRollout) | SQL Server 2022 × 3 tenant DBs | Nonclustered columnstore index | `EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'OrderHistoryColumnstore' AND status = 'Ready')` |
| [MySQL Version-Gate](MySQL-VersionGate) | MySQL 8.0 ↔ MySQL 9 | `VECTOR(384)` column | `CAST(SUBSTRING_INDEX(VERSION(), '.', 1) AS UNSIGNED) >= 9` |

Each demo is self-contained: schema package, `docker-compose.yml` with the engine pair (or single instance + multi-DB for the SQL Server one), launcher scripts (`run-demo.sh` / `run-demo.cmd`), and a one-page README with the expected output.

## Running a demo

```bash
cd PostgreSQL-VersionGate      # or MySQL-VersionGate / SqlServer-RollingRollout
./run-demo.sh                  # macOS / Linux
run-demo.cmd                   # Windows
```

Each launcher publishes the SchemaQuench binary (via the existing `build-schemaquench.sh` / `.cmd` at the repo root), brings up the engines under `docker compose`, runs the deploy, and emits a verification block showing what landed where.

Cleanup is `docker compose down --volumes` from inside the demo directory.

## What you're seeing

Two **version-gate** shapes (Postgres, MySQL) and one **state-gate** shape (SQL Server):

- **Version-gate:** the same JSON column declaration carries an engine-version expression. Older engine evaluates it as false and skips the column; newer engine evaluates it as true and applies the column. The deploy artifact is identical; only the target engine's version differs.
- **State-gate:** the same JSON index declaration carries an `EXISTS`-against-control-table expression. The "row" in `dbo.RolloutControl` is the DBA's per-database approval lever — flip it to `Ready` when a given tenant is approved for the heavy DDL in this maintenance window, leave it `Pending` otherwise. Same schema package on every deploy run; only the tenants the DBA has approved take on the index in any given window.

Both shapes are exactly the conditional-deployment pattern described in the article *The Production Server That Can't Be Upgraded — and the Deployment Pipeline That Has to Live With It* (LinkedIn, 2026-06-11). The patterns recur across teams and stacks; SchemaSmith's role is to put the conditional logic **in the schema package**, in the language the target engine already speaks, instead of in a CI wrapper.

## Article

This is the demo bundle for the article. Open the article alongside the demos and the three "Run any of them in under ten minutes" claims are the demos in this directory.
