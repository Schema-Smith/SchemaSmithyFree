# PostgreSQL Version-Gate Demo

One schema package. Two PostgreSQL versions. Different deployed shapes — driven by the schema package, not by the CI script.

## What's in here

A small SchemaSmith schema package — three tables (`Customers`, `Orders`, `OrderEvents`) — designed to deploy successfully against **PostgreSQL 15** and **PostgreSQL 18** (the current major) from the same package. 15 is this demo's lower rung, chosen to straddle the gate — not a SchemaSmith limit; SchemaSmith supports PostgreSQL 12 and up.

The catch: `OrderEvents.Status` is a **virtual generated column** that extracts a path out of the `Payload` JSONB. Virtual generated columns landed in PostgreSQL 18; PG15 doesn't have them. So the column carries a `ShouldApplyExpression` the engine itself evaluates at deploy time:

```json
{
  "Name": "Status",
  "DataType": "TEXT",
  "Nullable": true,
  "Generated": "ALWAYS",
  "GenerationExpression": "(Payload->>'status')",
  "Virtual": true,
  "ShouldApplyExpression": "current_setting('server_version_num')::int >= 180000"
}
```

PG15 evaluates `current_setting('server_version_num')::int >= 180000` as **false** and skips the column. PG18 evaluates it as **true** and applies the column with `attgenerated = 'v'` (the marker that confirms it's a true virtual generated column, not a stored fallback).

## Running it

```bash
./run-demo.sh        # macOS / Linux
run-demo.cmd         # Windows
```

The launcher publishes SchemaQuench, brings up `docker compose`, and runs the deploy against both PG containers in parallel. When both are done, a final verification service prints both schemas side by side so you can see the gating happen.

Expected output (the bottom half — see the launcher console for the full run):

```
--- PG15 (server_version_num < 180000): Status column SKIPPED ---
 column_name | data_type | is_generated | generation_expression
-------------+-----------+--------------+-----------------------
 EventId     | bigint    | NEVER        |
 OccurredAt  | timestamp with time zone | NEVER |
 Payload     | jsonb     | NEVER        |

--- PG18 (server_version_num >= 180000): Status column APPLIED ---
    attgenerated = "v" confirms the column is truly VIRTUAL
 column_name | data_type | is_generated |    generation_expression    | gen_marker
-------------+-----------+--------------+-----------------------------+------------
 EventId     | bigint    | NEVER        |                             |
 OccurredAt  | ...       | NEVER        |                             |
 Payload     | jsonb     | NEVER        |                             |
 Status      | text      | ALWAYS       | (Payload ->> 'status'::text)| v
```

## Why this matters

Most schema-management tools handle version differences by **branching the deployment pipeline** — environment-specific CI scripts, version-aware wrappers, hand-maintained "only on v18+" comment blocks. The decision about whether to apply a change ends up living *next to* the migration, not *in* it.

SchemaSmith flips that. The condition lives **in the package**, evaluated by the target engine at deploy time. The CI pipeline is the same on PG15 and PG18. The deploy artifact is the same. The intelligence is in the SQL, not in the YAML.

When this pattern works on every component type that supports `ShouldApplyExpression` (columns, indexes, foreign keys, check constraints, etc.), engine-version drift across your environments stops being a tax on every PR.

## Cleanup

```bash
docker compose down --volumes
```

## Related

- Article: *The Production Server That Can't Be Upgraded — and the Deployment Pipeline That Has to Live With It* (LinkedIn, 2026-06-11)
- Sister demos:
  - [`../SqlServer-RollingRollout`](../SqlServer-RollingRollout) — rolling NCCI rollout across tenant databases (state-based gating, not version-based)
  - [`../MySQL-VersionGate`](../MySQL-VersionGate) — same version-gate shape on MySQL, gating a `VECTOR` column on MySQL 9+
