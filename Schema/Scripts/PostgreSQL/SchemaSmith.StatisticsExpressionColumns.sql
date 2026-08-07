-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."StatisticsExpressionColumns"(p_schema TEXT, p_stxname TEXT) RETURNS TEXT[]
    LANGUAGE plpgsql STABLE
AS $$
DECLARE
  v_result TEXT[];
BEGIN
  -- pg_stats_ext_exprs is a PostgreSQL 14 system view (expression-based extended statistics). Below 14 the
  -- view does not exist — a static reference is a plan-time "relation does not exist" even in an untaken
  -- branch — and no expression statistics can exist there, so the result is an empty array. It is read via
  -- EXECUTE so the view is referenced only at runtime on a server that actually has it, including from the
  -- compare-side serialization and the existing-statistics snapshot. Keyed on the REAL server version (a
  -- physical relation-existence question), not the override-aware ServerVersionNum().
  IF (current_setting('server_version_num')::int / 10000) >= 14 THEN
    EXECUTE 'SELECT ARRAY_AGG(exp.expr) FROM pg_stats_ext_exprs exp WHERE exp.schemaname = $1 AND exp.statistics_name = $2'
      INTO v_result USING p_schema, p_stxname;
  END IF;
  RETURN COALESCE(v_result, ARRAY[]::text[]);
END $$;
