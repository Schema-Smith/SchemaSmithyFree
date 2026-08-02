-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR ALTER FUNCTION SchemaSmith.fn_ServerMajorVersion()
  RETURNS INT
AS
BEGIN
  -- The C# kindler bakes the detected server major version into {{ServerMajorVersion}} at kindle time
  -- (0 when no caller supplies one). SESSION_CONTEXT (the former override transport) is 2016+ and would
  -- fail to CREATE this function on the 2008 / compat-100 floor, so it is no longer read. When nothing is
  -- baked (0) fall back to the real server property so modern targets behave unchanged; a baked value wins.
  -- SERVERPROPERTY('ProductMajorVersion') is itself unavailable pre-2016 (returns NULL there), which is
  -- exactly why the C#-baked value is the authoritative source on genuine old binaries.
  RETURN COALESCE(NULLIF({{ServerMajorVersion}}, 0), CONVERT(INT, SERVERPROPERTY('ProductMajorVersion')))
END
