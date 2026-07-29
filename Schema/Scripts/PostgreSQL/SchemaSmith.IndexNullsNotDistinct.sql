-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."IndexNullsNotDistinct"(p_indexrelid OID) RETURNS BOOLEAN
    LANGUAGE plpgsql STABLE
AS $$
DECLARE
  v_result BOOLEAN;
BEGIN
  -- pg_index.indnullsnotdistinct is a PostgreSQL 15 column; below 15 the property does not exist and
  -- is always false. It is read via EXECUTE so the column is referenced only at runtime on a server
  -- that actually has it — a static reference is a plan-time 42703 on an older server, even from
  -- SchemaTongs extraction. Keyed on the REAL server version (a physical column-existence question),
  -- not the override-aware ServerVersionNum().
  IF (current_setting('server_version_num')::int / 10000) >= 15 THEN
    EXECUTE 'SELECT indnullsnotdistinct FROM pg_index WHERE indexrelid = $1' INTO v_result USING p_indexrelid;
    RETURN COALESCE(v_result, FALSE);
  END IF;
  RETURN FALSE;
END $$;
