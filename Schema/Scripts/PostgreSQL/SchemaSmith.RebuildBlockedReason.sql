-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Answers "why can this table NOT be rebuilt?" -- a short reason naming the blocking state, or NULL when a
-- rebuild is safe. A rebuild is a shadow-copy-and-swap, and each state below lives OUTSIDE the definition a
-- schema package carries, so the copy silently destroys it and the package cannot put it back: a publication
-- loses the article (and every subscriber's stream with it), an inheritance or partition edge is severed, and
-- a partitioned parent has no rows of its own to copy in the first place. Fail closed and leave those tables
-- to Before/After migration scripts.
--
-- A reason string rather than a boolean so the caller can name the state instead of saying "cannot"; a
-- function rather than inline logic so each state is verifiable on its own.
--
-- Every catalog object read here exists at the PostgreSQL 12 floor: pg_publication_tables, pg_class.relkind
-- 'p' and pg_class.relispartition all arrived with declarative partitioning and logical replication in
-- PostgreSQL 10, and pg_inherits predates all of them. So none of these needs the runtime EXECUTE staging
-- that SchemaSmith.ColumnCompression / SchemaSmith.IndexNullsNotDistinct use for genuinely newer columns.
CREATE OR REPLACE FUNCTION "SchemaSmith"."RebuildBlockedReason"(p_Schema TEXT, p_Table TEXT) RETURNS TEXT
    LANGUAGE plpgsql STABLE
AS $$
DECLARE
  v_oid OID;
  v_relkind "char";
BEGIN
  SELECT c.oid, c.relkind
  INTO v_oid, v_relkind
  FROM pg_catalog.pg_class c
  JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
  WHERE n.nspname = p_Schema
    AND c.relname = p_Table
    AND c.relkind IN ('r', 'p');

  -- Unknown table: there is nothing to rebuild, so nothing blocks one. The caller decides what a missing
  -- table means; this function does not invent a blocking reason for it.
  IF v_oid IS NULL THEN
    RETURN NULL;
  END IF;

  -- Partitioning is checked before inheritance because a partition is ALSO a pg_inherits child: reporting a
  -- leaf as a plain inheritance child would name the wrong state in the operator's error message.
  IF v_relkind = 'p' THEN
    RETURN 'the table is a partitioned table';
  END IF;

  IF EXISTS (SELECT 1 FROM pg_catalog.pg_class c WHERE c.oid = v_oid AND c.relispartition) THEN
    RETURN 'the table is a partition of a partitioned table';
  END IF;

  IF EXISTS (SELECT 1 FROM pg_catalog.pg_inherits i WHERE i.inhrelid = v_oid) THEN
    RETURN 'the table inherits from a parent table';
  END IF;

  IF EXISTS (SELECT 1 FROM pg_catalog.pg_inherits i WHERE i.inhparent = v_oid) THEN
    RETURN 'the table has child tables that inherit from it';
  END IF;

  -- pg_publication_tables resolves FOR ALL TABLES / FOR TABLES IN SCHEMA publications as well as
  -- individually-added articles, which pg_publication_rel alone would miss.
  IF EXISTS (SELECT 1 FROM pg_catalog.pg_publication_tables pt
             WHERE pt.schemaname = p_Schema AND pt.tablename = p_Table) THEN
    RETURN 'the table is a member of a logical replication publication';
  END IF;

  RETURN NULL;
END $$;
