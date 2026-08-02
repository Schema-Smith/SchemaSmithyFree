# MariaDB Version-Gate Demo

One schema package. Two MariaDB versions. Different deployed shapes — driven by the schema package, not by the CI script.

## What's in here

A small SchemaSmith schema package — two tables (`Assets`, `AssetIdentifiers`) — that deploys successfully against **MariaDB 10.6** (the SchemaSmith MariaDB floor) and **MariaDB 11.4** (current LTS) from the same package.

The catch: `AssetIdentifiers.ExternalUuid` uses MariaDB's native `UUID` data type, which landed in **MariaDB 10.7**. It does not exist in 10.6. So the column carries a `ShouldApplyExpression` the engine itself evaluates at deploy time:

```json
{
  "Name": "ExternalUuid",
  "DataType": "UUID",
  "Nullable": true,
  "VariantName": "UUID native type (MariaDB 10.7+)",
  "ShouldApplyExpression": "CAST(SUBSTRING_INDEX(VERSION(), '.', 1) AS UNSIGNED) * 100 + CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(VERSION(), '.', 2), '.', -1) AS UNSIGNED) >= 1007"
}
```

### Why the predicate looks like that — the mid-major trap

The [MySQL sister demo](../MySQL-VersionGate) gets away with comparing only the **major** version, because `VECTOR` arrived exactly on the 9.0 boundary. That is the exception, not the rule.

`UUID` arrived at **10.7** — *mid-major*. A major-only gate is wrong in both directions:

| Server | `major >= 11` (wrong) | major·100 + minor `>= 1007` (right) |
|---|---|---|
| 10.6 | false ✅ | false ✅ |
| **10.7 – 10.11** | **false ❌ — silently skips a supported feature** | true ✅ |
| 11.4 | true ✅ | true ✅ |

So the predicate folds major and minor into one comparable number: `10.6` → `1006`, `10.7` → `1007`, `11.4` → `1104`. Most engine features land mid-major, which makes this the shape you will reach for most often.

## Running it

```bash
./run-demo.sh        # macOS / Linux
run-demo.cmd         # Windows
```

The launcher publishes SchemaQuench, brings up `docker compose`, and runs the deploy against both MariaDB containers in parallel. When both finish, a verification service prints both `AssetIdentifiers` schemas.

Actual output (the bottom half):

```
--- MariaDB 10.6 (below 10.7): ExternalUuid column SKIPPED ---
server
10.6.27-MariaDB-ubu2204
Field           Type            Null    Key     Default Extra
IdentifierId    bigint(20)      NO      PRI     NULL
AssetId         bigint(20)      NO      MUL     NULL
Scheme          varchar(64)     NO              NULL

--- MariaDB 11.4 (10.7 or later): ExternalUuid APPLIED as native UUID ---
server
11.4.12-MariaDB-ubu2404
Field           Type            Null    Key     Default Extra
IdentifierId    bigint(20)      NO      PRI     NULL
AssetId         bigint(20)      NO      MUL     NULL
Scheme          varchar(64)     NO              NULL
ExternalUuid    uuid            YES             NULL
```

Negative control: run `CREATE TABLE foo (id UUID);` against MariaDB 10.6 directly — it fails with `ERROR 4161 (HY000): Unknown data type: 'UUID'`. The feature gap is real; the demo shows SchemaSmith handling it without a version check anywhere in the CI script.

## Trying other version pairs

Both images are overridable in [`.env`](.env) — `MARIADB_OLD_IMAGE` and `MARIADB_NEW_IMAGE`. Keep the old side below 10.7 and the new side at or above it, or both sides behave identically and the demo stops demonstrating anything. The compose service names (`mariadb10`, `mariadb11`) reflect the defaults, not your override.

A worthwhile experiment: set `MARIADB_OLD_IMAGE="mariadb:10.11"`. A major-only gate would wrongly skip the column there; this demo's gate correctly applies it.

## Why this matters

Fleets rarely sit on one MariaDB minor. If some boxes are on 10.6 (still in support until mid-2026) and others are on 10.11 or 11.4, and you want native `UUID` storage where it exists, the conventional answer is to fork the migration files or wrap them in shell version checks. With `ShouldApplyExpression` the version check lives **in the schema package**, evaluated by the target engine at deploy time. Same CI script, same deploy artifact, different schemas exactly where the engine supports the difference.

## Cleanup

```bash
docker compose down --volumes
```

## Related

- Sister demos:
  - [`../MySQL-VersionGate`](../MySQL-VersionGate) — the same shape on MySQL, gating a `VECTOR(384)` column on MySQL 9+ (major-only gate)
  - [`../PostgreSQL-VersionGate`](../PostgreSQL-VersionGate) — gating a virtual generated column on PG18+
  - [`../SqlServer-CompatLevelGate`](../SqlServer-CompatLevelGate) — gating on database **compatibility level** rather than server version
  - [`../SqlServer-RollingRollout`](../SqlServer-RollingRollout) — state-based gating (a DBA approval row), not version-based
