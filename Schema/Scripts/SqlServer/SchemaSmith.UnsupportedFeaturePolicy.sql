-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
CREATE OR ALTER FUNCTION SchemaSmith.UnsupportedFeaturePolicy()
  RETURNS VARCHAR(4)
AS
BEGIN
  -- Policy for a model feature the DETECTED target version/compat cannot support: 'warn' (default)
  -- emits the degraded form plus a 'downgraded' ChangeAudit manifest row; 'fail' aborts. Set per
  -- connection by DatabaseQuench.GetConnection from Target:UnsupportedFeaturePolicy via
  -- sp_set_session_context, mirroring the PostgreSQL schemasmith.unsupported_policy GUC. Any value
  -- other than an explicit 'fail' resolves to the safe 'warn' default.
  RETURN CASE WHEN LOWER(ISNULL(CONVERT(VARCHAR(4), SESSION_CONTEXT(N'schemasmith.unsupported_policy')), 'warn')) = 'fail'
              THEN 'fail' ELSE 'warn' END
END
