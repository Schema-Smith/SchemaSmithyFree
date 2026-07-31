-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR ALTER FUNCTION SchemaSmith.fn_ServerMajorVersion()
  RETURNS INT
AS
BEGIN
  -- Override (test affordance) wins; otherwise the real server major version.
  -- ISNUMERIC-guarded CONVERT rather than TRY_CONVERT: TRY_CONVERT requires compatibility level 110+ and is
  -- a parse error at the 2008 / compat-100 floor. The override is always an integer when set.
  DECLARE @v_Override NVARCHAR(50) = CONVERT(NVARCHAR(50), SESSION_CONTEXT(N'schemasmith.version_override'))
  RETURN COALESCE(CASE WHEN ISNUMERIC(@v_Override) = 1 THEN CONVERT(INT, @v_Override) END,
                  CONVERT(INT, SERVERPROPERTY('ProductMajorVersion')))
END
