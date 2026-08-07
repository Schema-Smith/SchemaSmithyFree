-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."ColumnCompression"(p_attrelid OID, p_attnum SMALLINT) RETURNS TEXT
    LANGUAGE plpgsql STABLE
AS $$
DECLARE
  v_compression "char";
BEGIN
  -- pg_attribute.attcompression is a PostgreSQL 14 column (per-column TOAST compression); below 14 the
  -- property does not exist and there is no per-column compression, so the serialized form is 'DEFAULT'
  -- (matching how a PG14+ column with no explicit compression serializes). It is read via EXECUTE so the
  -- column is referenced only at runtime on a server that actually has it — a static reference is a
  -- plan-time 42703 on an older server, including from SchemaTongs extraction. Keyed on the REAL server
  -- version (a physical column-existence question), not the override-aware ServerVersionNum().
  IF (current_setting('server_version_num')::int / 10000) >= 14 THEN
    EXECUTE 'SELECT attcompression FROM pg_attribute WHERE attrelid = $1 AND attnum = $2'
      INTO v_compression USING p_attrelid, p_attnum;
    RETURN CASE v_compression WHEN 'p' THEN 'pglz' WHEN 'l' THEN 'lz4' ELSE 'DEFAULT' END;
  END IF;
  RETURN 'DEFAULT';
END $$;
