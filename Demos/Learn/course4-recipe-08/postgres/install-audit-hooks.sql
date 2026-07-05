-- Recipe 6 installed the SIMPLEST recyclebin hook: rename the table aside, rename it back. This recipe
-- AUTHORS a richer, production-honest body against the same contract. On top of the audit trail, the drop
-- hook does the two things a real soft-drop must do before it sets a table aside:
--   * STRIP the table's own constraints and indexes. A PostgreSQL PK/UNIQUE constraint owns an index whose
--     name is schema-scoped, so an archived copy that kept it would collide the next time a table of the
--     same name is created. The engine re-adds them from the model when the table is restored.
--   * CLEAR the product-ownership rows, so the archived copy (and its stale original-name entry) isn't
--     tracked as owned on the next quench.
-- The engine already drops INBOUND foreign keys before calling the hook. Retention, row count, and who/when
-- go to the audit table, which doubles as the restore registry. Run once (KindleTheForge makes the schema).
CREATE SCHEMA IF NOT EXISTS "SchemaSmith";

CREATE TABLE IF NOT EXISTS "SchemaSmith"."TableDropAudit" (
  audit_id       BIGSERIAL PRIMARY KEY,
  schema_name    TEXT        NOT NULL,
  table_name     TEXT        NOT NULL,
  archived_name  TEXT,
  rows_archived  BIGINT,
  retention_days INT,
  action         TEXT        NOT NULL,          -- 'DROP' | 'RESTORE'
  action_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  action_by      TEXT        NOT NULL DEFAULT current_user
);

CREATE OR REPLACE PROCEDURE "SchemaSmith"."CustomTableDrop"(p_schema_name TEXT, p_table_name TEXT, p_retention_days INT DEFAULT 90)
LANGUAGE plpgsql AS $$
DECLARE
  v_archived TEXT;
  v_rows     BIGINT;
  r          RECORD;
BEGIN
  IF p_table_name LIKE '%\_\_dropped\_%' THEN RETURN; END IF;                          -- already archived
  IF to_regclass(format('%I.%I', p_schema_name, p_table_name)) IS NULL THEN RETURN; END IF;  -- already gone

  EXECUTE format('SELECT count(*) FROM %I.%I', p_schema_name, p_table_name) INTO v_rows;

  -- strip the table's own constraints (their PK/UNIQUE indexes carry schema-scoped names)
  FOR r IN SELECT conname FROM pg_constraint
           WHERE conrelid = format('%I.%I', p_schema_name, p_table_name)::regclass LOOP
    EXECUTE format('ALTER TABLE %I.%I DROP CONSTRAINT %I', p_schema_name, p_table_name, r.conname);
  END LOOP;
  -- and any standalone indexes left behind (also schema-scoped names)
  FOR r IN SELECT indexname FROM pg_indexes WHERE schemaname = p_schema_name AND tablename = p_table_name LOOP
    EXECUTE format('DROP INDEX IF EXISTS %I.%I', p_schema_name, r.indexname);
  END LOOP;

  -- clear the ownership rows so nothing is tracked under the old name
  DELETE FROM "SchemaSmith"."ProductOwnership" WHERE "Schema" = p_schema_name AND "TableName" = p_table_name;

  v_archived := p_table_name || '__dropped_' || to_char(clock_timestamp(), 'YYYYMMDDHH24MISSMS');
  EXECUTE format('ALTER TABLE %I.%I RENAME TO %I', p_schema_name, p_table_name, v_archived);

  INSERT INTO "SchemaSmith"."TableDropAudit"(schema_name, table_name, archived_name, rows_archived, retention_days, action)
  VALUES (p_schema_name, p_table_name, v_archived, v_rows, p_retention_days, 'DROP');
END $$;

CREATE OR REPLACE PROCEDURE "SchemaSmith"."CustomTableRestore"(p_schema_name TEXT, p_table_name TEXT)
LANGUAGE plpgsql AS $$
DECLARE
  v_archived TEXT;
BEGIN
  SELECT archived_name INTO v_archived
  FROM "SchemaSmith"."TableDropAudit"
  WHERE schema_name = p_schema_name AND table_name = p_table_name AND action = 'DROP'
  ORDER BY action_at DESC LIMIT 1;

  IF v_archived IS NULL THEN RETURN; END IF;                                        -- never soft-dropped
  IF to_regclass(format('%I.%I', p_schema_name, v_archived)) IS NULL THEN RETURN; END IF;  -- already restored

  EXECUTE format('ALTER TABLE %I.%I RENAME TO %I', p_schema_name, v_archived, p_table_name);
  INSERT INTO "SchemaSmith"."TableDropAudit"(schema_name, table_name, archived_name, action)
  VALUES (p_schema_name, p_table_name, v_archived, 'RESTORE');
END $$;
