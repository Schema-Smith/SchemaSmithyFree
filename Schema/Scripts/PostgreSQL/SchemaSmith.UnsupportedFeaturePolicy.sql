-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."UnsupportedFeaturePolicy"() RETURNS TEXT
    LANGUAGE sql STABLE
AS $$
  -- Policy for a model feature the DETECTED target version cannot support: 'warn' (default) emits the
  -- degraded form plus a downgrade manifest row; 'fail' aborts. Set per-connection by ProductQuench from
  -- Target:UnsupportedFeaturePolicy (session GUC, mirroring schemasmith.version_override). Any value
  -- other than an explicit 'fail' resolves to the safe 'warn' default.
  SELECT CASE WHEN LOWER(COALESCE(NULLIF(current_setting('schemasmith.unsupported_policy', true), ''), 'warn')) = 'fail'
              THEN 'fail' ELSE 'warn' END;
$$;
