-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Lightweight bootstrap procedure with ZERO SchemaSmith_* table or proc dependencies.
-- Parses a TableQuench-shaped JSON definition and applies:
--   1. CREATE TABLE IF NOT EXISTS (built from Columns + any PrimaryKey index)
--   2. ALTER TABLE ADD COLUMN IF NOT EXISTS per missing column
--   3. CREATE INDEX IF NOT EXISTS per missing non-PK index
-- Out of scope: column type changes, drops, FKs, check constraints, ownership tracking.
-- Idempotent: a second call on the same definition is a no-op.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."BootstrapTableQuench"
  (p_TableDefinitions TEXT)
  LANGUAGE plpgsql
AS $$
DECLARE
    v_def JSONB := p_TableDefinitions::jsonb;
    v_schema TEXT;
    v_name TEXT;
    v_column_list TEXT := '';
    v_pk_clause TEXT := '';
    v_sql TEXT;
    v_col JSONB;
    v_idx JSONB;
    v_col_name TEXT;
    v_col_type TEXT;
    v_col_nullable BOOLEAN;
    v_col_default TEXT;
    v_idx_name TEXT;
    v_idx_cols TEXT;
BEGIN
    v_schema := TRIM(BOTH FROM (v_def->>'Schema'));
    v_name := TRIM(BOTH FROM (v_def->>'Name'));

    IF v_schema IS NULL OR v_schema = '' OR v_name IS NULL OR v_name = '' THEN
        RAISE EXCEPTION 'BootstrapTableQuench: JSON must contain non-blank Schema and Name.';
    END IF;

    -- Step 1: CREATE TABLE IF NOT EXISTS, built from Columns + optional inline PK.
    -- We always emit CREATE TABLE IF NOT EXISTS — it is a no-op against existing tables.
    FOR v_col IN SELECT * FROM jsonb_array_elements(v_def->'Columns')
    LOOP
        v_col_name := v_col->>'Name';
        v_col_type := v_col->>'DataType';
        v_col_nullable := COALESCE((v_col->>'Nullable')::boolean, false);
        v_col_default := v_col->>'Default';

        IF v_column_list <> '' THEN
            v_column_list := v_column_list || ', ';
        END IF;
        v_column_list := v_column_list || '"' || v_col_name || '" ' || v_col_type ||
                         CASE WHEN v_col_nullable THEN ' NULL' ELSE ' NOT NULL' END ||
                         CASE WHEN COALESCE(TRIM(v_col_default), '') <> '' THEN ' DEFAULT ' || v_col_default ELSE '' END;
    END LOOP;

    -- First PrimaryKey index goes inline at CREATE TABLE time as a PRIMARY KEY constraint.
    SELECT idx INTO v_idx
      FROM jsonb_array_elements(v_def->'Indexes') idx
      WHERE COALESCE((idx->>'PrimaryKey')::boolean, false) = true
      LIMIT 1;

    IF v_idx IS NOT NULL THEN
        v_idx_name := v_idx->>'Name';
        v_idx_cols := v_idx->>'IndexColumns';
        v_pk_clause := ', CONSTRAINT "' || v_idx_name || '" PRIMARY KEY (' || v_idx_cols || ')';
    END IF;

    v_sql := 'CREATE TABLE IF NOT EXISTS "' || v_schema || '"."' || v_name || '" (' || v_column_list || v_pk_clause || ')';
    EXECUTE v_sql;

    -- Step 2: ADD COLUMN IF NOT EXISTS folded into one multi-clause ALTER (PG has native
    -- IF NOT EXISTS support for ADD COLUMN since 9.6). Declared-column order preserved.
    SELECT string_agg(
             'ADD COLUMN IF NOT EXISTS "' || (col->>'Name') || '" ' || (col->>'DataType') ||
             CASE WHEN COALESCE((col->>'Nullable')::boolean, false) THEN ' NULL' ELSE ' NOT NULL' END ||
             CASE WHEN COALESCE(TRIM(col->>'Default'), '') <> '' THEN ' DEFAULT ' || (col->>'Default') ELSE '' END,
             ', ' ORDER BY ord)
      INTO v_sql
      FROM jsonb_array_elements(v_def->'Columns') WITH ORDINALITY AS t(col, ord);
    IF v_sql IS NOT NULL THEN
        EXECUTE 'ALTER TABLE "' || v_schema || '"."' || v_name || '" ' || v_sql;
    END IF;

    -- Step 3: CREATE INDEX IF NOT EXISTS for non-PK indexes, folded into one batch
    -- (PG supports IF NOT EXISTS natively and runs a multi-statement EXECUTE string). Order preserved.
    SELECT string_agg(
             'CREATE ' ||
             CASE WHEN COALESCE((idx->>'Unique')::boolean, false) THEN 'UNIQUE ' ELSE '' END ||
             'INDEX IF NOT EXISTS "' || (idx->>'Name') || '" ON "' ||
             v_schema || '"."' || v_name || '" (' || (idx->>'IndexColumns') || ')',
             '; ' ORDER BY ord)
      INTO v_sql
      FROM jsonb_array_elements(v_def->'Indexes') WITH ORDINALITY AS t(idx, ord)
      WHERE COALESCE((idx->>'PrimaryKey')::boolean, false) = false;
    IF v_sql IS NOT NULL THEN
        EXECUTE v_sql;
    END IF;
END $$;
