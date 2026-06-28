-- Installs the two recyclebin hooks SchemaQuench looks for. When a table is removed from the product,
-- the engine routes its drop through "SchemaSmith"."CustomTableDrop" (if present) instead of a hard
-- DROP; when a table is being added, it calls "SchemaSmith"."CustomTableRestore" first and, if the
-- table comes back, does not recreate it. These hooks "soft-drop" by renaming the table aside, so its
-- structure AND data ride through the rebuild. Run once (KindleTheForge creates the SchemaSmith schema).
CREATE SCHEMA IF NOT EXISTS "SchemaSmith";

CREATE OR REPLACE PROCEDURE "SchemaSmith"."CustomTableDrop"(p_Schema TEXT, p_Table TEXT) LANGUAGE plpgsql AS $$
DECLARE
  rb TEXT := '__recyclebin__' || p_Table;
BEGIN
  IF left(p_Table, 14) = '__recyclebin__' THEN RETURN; END IF;  -- never recycle a recyclebin table
  EXECUTE format('DROP TABLE IF EXISTS %I.%I', p_Schema, rb);
  EXECUTE format('ALTER TABLE %I.%I RENAME TO %I', p_Schema, p_Table, rb);
END $$;

CREATE OR REPLACE PROCEDURE "SchemaSmith"."CustomTableRestore"(p_Schema TEXT, p_Table TEXT) LANGUAGE plpgsql AS $$
DECLARE
  rb TEXT := '__recyclebin__' || p_Table;
BEGIN
  IF to_regclass(format('%I.%I', p_Schema, rb)) IS NOT NULL THEN
    EXECUTE format('ALTER TABLE %I.%I RENAME TO %I', p_Schema, rb, p_Table);
  END IF;
END $$;
