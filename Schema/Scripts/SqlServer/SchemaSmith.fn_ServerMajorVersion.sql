-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR ALTER FUNCTION SchemaSmith.fn_ServerMajorVersion()
  RETURNS INT
AS
BEGIN
  -- Override (test affordance) wins; otherwise the real server major version.
  RETURN COALESCE(TRY_CONVERT(INT, SESSION_CONTEXT(N'schemasmith.version_override')),
                  CONVERT(INT, SERVERPROPERTY('ProductMajorVersion')))
END
