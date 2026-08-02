-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
CREATE OR ALTER FUNCTION SchemaSmith.UnsupportedFeaturePolicy()
  RETURNS VARCHAR(4)
AS
BEGIN
  -- Policy for a model feature the DETECTED target version/compat cannot support: 'warn' (default)
  -- emits the degraded form plus a 'downgraded' ChangeAudit manifest row; 'fail' aborts. The C# kindler
  -- bakes the resolved policy into {{UnsupportedPolicy}} at kindle time from Target:UnsupportedFeaturePolicy.
  -- SESSION_CONTEXT (the former transport) is 2016+ and would fail to CREATE this function on the 2008 /
  -- compat-100 floor, so it is no longer read; the PostgreSQL schemasmith.unsupported_policy GUC works on
  -- every PG version and keeps its runtime read. Any value other than an explicit 'fail' resolves to 'warn'.
  RETURN CASE WHEN LOWER('{{UnsupportedPolicy}}') = 'fail' THEN 'fail' ELSE 'warn' END
END
