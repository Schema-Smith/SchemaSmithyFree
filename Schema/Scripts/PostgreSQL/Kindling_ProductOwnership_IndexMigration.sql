-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- TRANSITIONAL (ProductOwnership one-owner enforcement)
-- Tightens "SchemaSmith"."ProductOwnership"'s unique key to (Schema, TableName, IndexName) with
-- NULLS NOT DISTINCT, so a physical object -- a table (NULL IndexName) or an index -- can have
-- exactly ONE owner row. This is structural parity with SQL Server (a single ProductName extended
-- property per object) and MySQL (uk_object on ObjectType/ObjectSchema/ObjectName). The prior PG key
-- included ProductName + template_name, which structurally permitted multiple owner rows for one
-- object; multi-owner is an invalid state that should never exist.
--
-- BootstrapTableQuench only CREATEs an index by name IF NOT EXISTS and never reconciles a changed
-- definition, so this one-time migration drops the old-shape index and creates the tightened one.
-- Runs AFTER Kindling_ProductOwnership_Table.sql (the table exists by now). Idempotent: once the
-- tightened NULLS NOT DISTINCT index is in place it neither drops nor recreates.
--
-- FAILS LOUD if pre-existing dual-owner rows exist (CREATE UNIQUE INDEX errors). Going forward the
-- tightened key plus FixupTableOwnership's now-template-agnostic insert guard prevent new dual rows,
-- but a database migrated from the old 5-column key COULD carry cross-template dual rows created
-- before this change -- failing loud surfaces that real invariant violation for manual reconciliation
-- rather than silently picking a winner.
--
-- DELETION TRIGGER: once all deployed databases are presumed migrated (~2 releases past the release
-- that ships this), delete this script and its ForgeKindler PostgreSQL entry (the tightened index is
-- then the only shape any live database has). Tracked in the Community roadmap.
--
-- VERSION-ADAPTIVE (PG floor-lowering): NULLS NOT DISTINCT is a PG15 feature. Below 15 the identical
-- one-owner invariant is enforced with a functional unique index on COALESCE("IndexName", '') — a
-- NULL IndexName (a table) folds to '' so it collides with itself. An index can never be named '', so
-- the two key spaces (tables vs named indexes) never cross. The tightened form for the running server
-- is therefore NULLS NOT DISTINCT on >= 15 and the COALESCE expression on < 15.
DO $$
DECLARE
  -- Real server capability (not the override-aware helper): this creates a PHYSICAL index, so the
  -- actual server version decides the form — and reading server_version_num directly avoids a
  -- kindling-order dependency on ServerVersionNum() being kindled first.
  v_pg15 BOOLEAN := (current_setting('server_version_num')::int / 10000) >= 15;
  -- Marker that identifies the already-tightened form in pg_indexes.indexdef for THIS server version,
  -- so a repeat kindle neither drops nor recreates it (idempotent).
  v_tightened_marker TEXT := CASE WHEN v_pg15 THEN '%NULLS NOT DISTINCT%' ELSE '%COALESCE%' END;
BEGIN
  -- Drop any PK_ProductOwnership that is not already the tightened form for this server (the old
  -- 5-column key, or a 3-column key BootstrapTableQuench created without the one-owner enforcement).
  -- The old shape may be a plain unique index OR a constraint-backed index (a legacy PRIMARY KEY /
  -- UNIQUE constraint) — a constraint's backing index cannot be DROP INDEXed, so drop the constraint
  -- in that case (which drops its index), else drop the index directly.
  IF EXISTS (SELECT 1 FROM pg_indexes
              WHERE schemaname = 'SchemaSmith'
                AND indexname = 'PK_ProductOwnership'
                AND indexdef NOT ILIKE v_tightened_marker)
  THEN
    IF EXISTS (SELECT 1 FROM pg_constraint c
                 JOIN pg_namespace n ON n.oid = c.connamespace
                WHERE c.conname = 'PK_ProductOwnership'
                  AND n.nspname = 'SchemaSmith')
    THEN
      ALTER TABLE "SchemaSmith"."ProductOwnership" DROP CONSTRAINT "PK_ProductOwnership";
    ELSE
      DROP INDEX IF EXISTS "SchemaSmith"."PK_ProductOwnership";
    END IF;
  END IF;

  -- Create the tightened one-owner index if absent, in the form the running server supports. Built
  -- and run via EXECUTE so the PG15-only NULLS NOT DISTINCT clause is parsed only at runtime on a
  -- server that supports it — a static clause is a syntax error at plpgsql compile time on an older
  -- server even inside a never-taken branch.
  IF NOT EXISTS (SELECT 1 FROM pg_indexes
                  WHERE schemaname = 'SchemaSmith' AND indexname = 'PK_ProductOwnership')
  THEN
    EXECUTE 'CREATE UNIQUE INDEX "PK_ProductOwnership" ON "SchemaSmith"."ProductOwnership" ("Schema", "TableName", '
         || CASE WHEN v_pg15 THEN '"IndexName") NULLS NOT DISTINCT'
                 ELSE 'COALESCE("IndexName", ''''))' END;
  END IF;
END $$;
