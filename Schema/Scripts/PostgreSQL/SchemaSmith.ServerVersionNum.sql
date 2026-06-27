-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."ServerVersionNum"() RETURNS INTEGER
    LANGUAGE sql STABLE
AS $$
  -- Override (test affordance) wins; otherwise server_version_num (e.g. 160004) -> major 16.
  SELECT COALESCE(NULLIF(current_setting('schemasmith.version_override', true), '')::int,
                  current_setting('server_version_num')::int / 10000);
$$;
