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
DO $$
BEGIN
  -- Drop any PK_ProductOwnership that is not already the tightened NULLS NOT DISTINCT form (the old
  -- 5-column key, or a 3-column key BootstrapTableQuench created without NULLS NOT DISTINCT). The
  -- old shape may be a plain unique index OR a constraint-backed index (a legacy PRIMARY KEY / UNIQUE
  -- constraint) — a constraint's backing index cannot be DROP INDEXed, so drop the constraint in that
  -- case (which drops its index), else drop the index directly.
  IF EXISTS (SELECT 1 FROM pg_indexes
              WHERE schemaname = 'SchemaSmith'
                AND indexname = 'PK_ProductOwnership'
                AND indexdef NOT ILIKE '%NULLS NOT DISTINCT%')
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

  -- Create the tightened one-owner index if absent. NULLS NOT DISTINCT makes a table's NULL IndexName
  -- collide with itself, so one physical object owns exactly one row.
  IF NOT EXISTS (SELECT 1 FROM pg_indexes
                  WHERE schemaname = 'SchemaSmith' AND indexname = 'PK_ProductOwnership')
  THEN
    CREATE UNIQUE INDEX "PK_ProductOwnership"
      ON "SchemaSmith"."ProductOwnership" ("Schema", "TableName", "IndexName") NULLS NOT DISTINCT;
  END IF;
END $$;
