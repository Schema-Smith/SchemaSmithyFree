# MySQL pt-osc + SchemaSmith Coexistence Demo

SchemaSmith declares the schema. pt-online-schema-change handles the heavy DDL. Neither tool steps on the other.

## What this demonstrates

On a table with millions of rows, a bare `ALTER TABLE ... ADD COLUMN` locks the table for as long as the operation runs. Teams running MySQL in production typically reach for `pt-online-schema-change` to apply that DDL online — copying rows in the background while production writes flow through triggers. This demo shows how SchemaSmith fits into that workflow: it deploys the initial schema, surfaces the exact ALTER clause via WhatIf, and then re-verifies the deployed state after pt-osc completes — correctly recognizing the pt-osc table-swap as matching declared state, with no phantom drift.

The two tools operate at different layers and hand off cleanly.

## Prerequisites

- Docker Desktop (or Docker Engine + Compose)
- ~5 GB free disk space for synthetic data
- About 5 minutes wall-clock to walk through the three acts

No local MySQL client or Percona Toolkit installation needed. Both are served from Docker images in `docker-compose.yml`.

## The three-act walkthrough

### Act 1: Deploy initial state

Bring up the containers and deploy the schema:

```bash
docker compose up -d mysql9 ptosc
docker compose run --rm quench
```

Expected output (SchemaSmith deploy — last few lines):

```
[mysql9].[ptosc_demo]     Create missing tables
[mysql9].[ptosc_demo]       Create table `OrderHistory`
[mysql9].[ptosc_demo]     Create missing indexes
[mysql9].[ptosc_demo]       Create index: `OrderHistory`.`ix_orderhistory_customer`
[mysql9].[ptosc_demo] Successfully Quenched
```

Verify the table structure:

```bash
docker compose exec -T mysql9 mysql -udemo_user -pdemo_password \
  -e "USE ptosc_demo; DESCRIBE OrderHistory;"
```

Expected:

```
+-------------+-------------+------+-----+---------+----------------+
| Field       | Type        | Null | Key | Default | Extra          |
+-------------+-------------+------+-----+---------+----------------+
| id          | bigint      | NO   | PRI | NULL    | auto_increment |
| customer_id | int         | NO   | MUL | NULL    |                |
| order_date  | datetime    | NO   |     | NULL    |                |
| total_cents | bigint      | NO   |     | NULL    |                |
| status      | varchar(16) | NO   |     | NULL    |                |
+-------------+-------------+------+-----+---------+----------------+
```

Load 2.5M synthetic rows — this takes about 90 seconds and represents a realistic large table:

```bash
docker compose exec -T mysql9 mysql -udemo_user -pdemo_password < load_data.sql
```

Where `load_data.sql` is:

```sql
USE ptosc_demo;

SET @load_start = NOW();

DROP PROCEDURE IF EXISTS LoadOrderHistory;

DELIMITER //
CREATE PROCEDURE LoadOrderHistory(IN target_rows BIGINT)
BEGIN
  DECLARE inserted_so_far BIGINT DEFAULT 0;
  DECLARE rows_per_batch INT DEFAULT 2000;
  DECLARE batches INT;
  SET batches = CEIL(target_rows / rows_per_batch);

  SET @batch = 0;
  WHILE @batch < batches DO
    INSERT INTO OrderHistory (customer_id, order_date, total_cents, status)
    SELECT
      FLOOR(RAND() * 100000),
      DATE_SUB(NOW(), INTERVAL FLOOR(RAND() * 1095) DAY),
      FLOOR(RAND() * 100000) + 100,
      ELT(FLOOR(RAND() * 4) + 1, 'Pending', 'Paid', 'Shipped', 'Cancelled')
    FROM information_schema.COLUMNS a
    CROSS JOIN information_schema.COLUMNS b
    LIMIT 2000;
    SET @batch = @batch + 1;
  END WHILE;
END//
DELIMITER ;

CALL LoadOrderHistory(2500000);
DROP PROCEDURE LoadOrderHistory;

SELECT TIMEDIFF(NOW(), @load_start) AS elapsed_time;
SELECT COUNT(*) AS final_row_count FROM OrderHistory;
```

Verify row count after loading:

```
final_row_count
2500000
```

### Act 2: Evolve declared state, hand off to pt-osc

Add a `shipping_zone` column to the schema package. Edit `Package/Templates/Main/Tables/OrderHistory.json`:

```json
    {
      "Name": "status",
      "DataType": "VARCHAR(16)"
    },
    {
      "Name": "shipping_zone",
      "DataType": "VARCHAR(16)",
      "Nullable": true
    }
```

Run SchemaSmith in WhatIf mode to see the proposed ALTER without applying it:

```bash
docker compose run --rm -e SmithySettings_WhatIfONLY=true quench
```

Expected output (the relevant line):

```
[mysql9].[ptosc_demo]     ALTER TABLE `ptosc_demo`.`OrderHistory` ADD COLUMN `shipping_zone` VARCHAR(16) NULL
```

That's the clause pt-osc needs. The part after `ALTER TABLE \`ptosc_demo\`.\`OrderHistory\` ` goes directly into `--alter`:

```bash
docker compose exec -T ptosc pt-online-schema-change \
  --alter "ADD COLUMN \`shipping_zone\` VARCHAR(16) NULL" \
  "h=mysql9,D=ptosc_demo,t=OrderHistory,u=demo_user,p=demo_password" \
  --execute \
  --no-check-replication-filters \
  --no-check-alter \
  --recursion-method=none
```

Expected output:

```
No replicas found.  See --recursion-method if host <id> has replicas.
Not checking replica lag because no replicas were found and --check-replica-lag was not specified.
Operation, tries, wait:
  analyze_table, 10, 1
  copy_rows, 10, 0.25
  create_triggers, 10, 1
  drop_triggers, 10, 1
  swap_tables, 10, 1
  update_foreign_keys, 10, 1
Altering `ptosc_demo`.`OrderHistory`...
Creating new table...
Created new table ptosc_demo._OrderHistory_new OK.
Altering new table...
Altered `ptosc_demo`.`_OrderHistory_new` OK.
2026-06-06T11:23:26 Creating triggers...
2026-06-06T11:23:26 Created triggers OK.
2026-06-06T11:23:26 Copying approximately 0 rows...
2026-06-06T11:23:52 Copied rows OK.
2026-06-06T11:23:52 Analyzing new table...
2026-06-06T11:23:52 Swapping tables...
2026-06-06T11:23:52 Swapped original and new tables OK.
2026-06-06T11:23:52 Dropping old table...
2026-06-06T11:23:52 Dropped old table `ptosc_demo`.`_OrderHistory_old` OK.
2026-06-06T11:23:52 Dropping triggers...
2026-06-06T11:23:52 Dropped triggers OK.
Successfully altered `ptosc_demo`.`OrderHistory`.
```

**~27 seconds wall-clock for 2.5M rows on a laptop.** A bare `ALTER TABLE` on the same table would hold a metadata lock for that duration; pt-osc copies rows in the background while production writes go through triggers.

Note: `"Copying approximately 0 rows"` is a cosmetic quirk — InnoDB row-count estimates are stale at pt-osc startup. All 2.5M rows are copied (confirmed by post-run row count).

### Act 3: SchemaSmith re-verifies

Re-run SchemaSmith against the post-pt-osc schema:

```bash
docker compose run --rm quench
```

Expected output (the key lines — no ALTER statements anywhere):

```
[mysql9].[ptosc_demo]     Create missing tables
[mysql9].[ptosc_demo]     Add missing columns to existing tables
[mysql9].[ptosc_demo]     Create missing indexes
[mysql9].[ptosc_demo] Successfully Quenched
```

No `ALTER TABLE`. No `DROP TABLE`. SchemaSmith reads the pt-osc table-swap result as identical to the declared state in `OrderHistory.json`. The handoff is clean in both directions.

## What's happening under the hood

The `docker-compose.yml` handles two environment details that any team running pt-osc against MySQL 9 will encounter:

**1. MySQL 9 + Percona Toolkit auth plugin compatibility.**
MySQL 9 dropped `mysql_native_password` — `caching_sha2_password` is the only plugin. The `perconalab/percona-toolkit` image ships a MySQL 8.0 client, which requires either SSL or `get_server_public_key=1` to connect over plaintext. The compose file volume-mounts `ptosc-config/.my.cnf` into `/root/.my.cnf` in the ptosc container:

```ini
[client]
get_server_public_key=1
```

Without this, pt-osc fails with an authentication error before reaching the ALTER.

**2. `REPLICATION CLIENT` grant.**
pt-osc runs `SHOW replica STATUS` at startup to check for replicas. MySQL requires `REPLICATION CLIENT` privilege for that query. The compose file mounts `mysql-init/01-grants.sql` into `/docker-entrypoint-initdb.d/` — MySQL's auto-exec directory for initialization scripts. The grant runs automatically on first container start. Without it, pt-osc aborts before doing any work.

**3. `--recursion-method=none` (this demo only).**
This flag is in the pt-osc command, not in any config file, because it's a per-invocation decision the reader needs to see. In a single-instance sandbox, pt-osc treats the absence of replicas as a possible misconfiguration and aborts unless you explicitly tell it not to look for replicas. `--recursion-method=none` suppresses that check. **Do not use this flag in production if you have replicas** — pt-osc's replica lag checks are the mechanism that keeps the ALTER from running ahead of replica apply.

## Cleanup

```bash
docker compose down -v
```

The `-v` removes the MySQL data volume, returning to a clean slate for the next run.

## About

This demo shows a tool-coexistence pattern on MySQL: SchemaSmith owns declarative schema state (what tables exist, what columns they have); `pt-online-schema-change` owns the online execution of heavy ALTERs. The two tools operate at different layers and hand off via the ALTER clause — SchemaSmith's WhatIf emits it, pt-osc runs it, SchemaSmith verifies the result matches declared state. Useful as a working reference and as a starting point for similar coexistence setups in your own environment.
