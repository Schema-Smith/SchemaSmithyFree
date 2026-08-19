-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.fn_ServerMajorVersion') IS NOT NULL DROP FUNCTION SchemaSmith.fn_ServerMajorVersion
GO
CREATE FUNCTION SchemaSmith.fn_ServerMajorVersion()
  RETURNS INT
AS
BEGIN
  -- Session override (test affordance), mirroring PostgreSQL's schemasmith.version_override GUC and
  -- MySQL's @schemasmith_version_override. Transported through CONTEXT_INFO rather than SESSION_CONTEXT:
  -- SESSION_CONTEXT is 2016+ and would fail to CREATE this function on the 2008 / compat-100 floor,
  -- while CONTEXT_INFO has existed since SQL Server 2000. The 0x53534F56 ('SSOV') prefix means a
  -- CONTEXT_INFO set by anything else is ignored rather than misread as a version.
  --   SET CONTEXT_INFO 0x53534F56 + CONVERT(BINARY(4), <major>)   -- force; 0x0 to clear
  DECLARE @v_Override VARBINARY(128) = CONTEXT_INFO()
  IF @v_Override IS NOT NULL AND SUBSTRING(@v_Override, 1, 4) = 0x53534F56
    RETURN CONVERT(INT, SUBSTRING(@v_Override, 5, 4))

  -- The C# kindler bakes the detected server major version into {{ServerMajorVersion}} at kindle time
  -- (0 when no caller supplies one). When nothing is baked (0) fall back to the real server property so
  -- modern targets behave unchanged; a baked value wins.
  -- SERVERPROPERTY('ProductMajorVersion') is itself unavailable pre-2016 (returns NULL there), which is
  -- exactly why the C#-baked value is the authoritative source on genuine old binaries.
  RETURN COALESCE(NULLIF({{ServerMajorVersion}}, 0), CONVERT(INT, SERVERPROPERTY('ProductMajorVersion')))
END
