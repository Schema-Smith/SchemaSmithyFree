# MySQL Version-Gate Demo

One schema package. Two MySQL versions. Different deployed shapes — driven by the schema package, not by the CI script.

## What's in here

A small SchemaSmith schema package — two tables (`Documents`, `Embeddings`) — designed to deploy successfully against **MySQL 8.0** (the SchemaSmith floor) and **MySQL 9** (current) from the same package.

The catch: `Embeddings.Embedding` is a `VECTOR(384)` column for storing model embeddings. The `VECTOR` data type landed in **MySQL 9** — it doesn't exist in MySQL 8.0. So the column carries a `ShouldApplyExpression` the engine itself evaluates at deploy time:

```json
{
  "Name": "Embedding",
  "DataType": "VECTOR(384)",
  "Nullable": true,
  "ShouldApplyExpression": "CAST(SUBSTRING_INDEX(VERSION(), '.', 1) AS UNSIGNED) >= 9"
}
```

MySQL 8.0 evaluates `major-version >= 9` as **false** and skips the column. MySQL 9 evaluates it as **true** and applies the column with the native `vector(384)` type.

## Running it

```bash
./run-demo.sh        # macOS / Linux
run-demo.cmd         # Windows
```

The launcher publishes SchemaQuench, brings up `docker compose`, and runs the deploy against both MySQL containers in parallel. When both are done, a final verification service prints both `Embeddings` table schemas side by side.

Expected output (the bottom half):

```
--- MySQL 8.0 (VERSION() major < 9): Embedding column SKIPPED ---
+-------------+------------+------+-----+---------+-------+
| Field       | Type       | Null | Key | Default | Extra |
+-------------+------------+------+-----+---------+-------+
| EmbeddingId | bigint     | NO   | PRI | NULL    |       |
| DocumentId  | bigint     | NO   | MUL | NULL    |       |
| Model       | varchar(64)| NO   |     | NULL    |       |
+-------------+------------+------+-----+---------+-------+

--- MySQL 9 (VERSION() major >= 9): Embedding column APPLIED as VECTOR(384) ---
+-------------+-------------+------+-----+---------+-------+
| Field       | Type        | Null | Key | Default | Extra |
+-------------+-------------+------+-----+---------+-------+
| EmbeddingId | bigint      | NO   | PRI | NULL    |       |
| DocumentId  | bigint      | NO   | MUL | NULL    |       |
| Model       | varchar(64) | NO   |     | NULL    |       |
| Embedding   | vector(384) | YES  |     | NULL    |       |
+-------------+-------------+------+-----+---------+-------+
```

Negative control: try `CREATE TABLE foo (e VECTOR(384));` against MySQL 8.0 directly — it errors with `You have an error in your SQL syntax`. The feature gap is real; the demo shows SchemaSmith handling it gracefully without manual version checks in the CI script.

## Why this matters

If your application footprint mixes MySQL 8.0 (LTS, still in long support through 2026) and MySQL 9 (innovation release), and you want to start using `VECTOR` columns for AI embeddings on the 9.x boxes, the conventional approach is to fork your migration files or wrap them in shell-script version checks. With SchemaSmith's `ShouldApplyExpression`, the version check lives **in the schema package**, evaluated by the target engine at deploy time. Same CI script. Same deploy artifact. Different schemas where the engine supports the difference.

## Cleanup

```bash
docker compose down --volumes
```

## Related

- Article: *The Production Server That Can't Be Upgraded — and the Deployment Pipeline That Has to Live With It* (LinkedIn, 2026-06-11)
- Sister demos:
  - [`../PostgreSQL-VersionGate`](../PostgreSQL-VersionGate) — same version-gate shape on PostgreSQL, gating a virtual generated column on PG18+
  - [`../SqlServer-RollingRollout`](../SqlServer-RollingRollout) — rolling NCCI rollout across tenant databases (state-based gating, not version-based)
