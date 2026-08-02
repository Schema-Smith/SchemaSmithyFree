-- Bootstrap the demo: two databases on ONE SQL Server instance, at different
-- database compatibility levels.
--
--   AppDb_Modern  -> COMPATIBILITY_LEVEL 160  -> GENERATE_SERIES available
--   AppDb_Legacy  -> COMPATIBILITY_LEVEL 130  -> GENERATE_SERIES NOT available
--
-- Both run on the same binary, so SERVERPROPERTY('ProductMajorVersion') returns 16
-- for BOTH. That is the entire point of the demo: the obvious server-version gate
-- cannot tell these two apart, and would wrongly deploy 160-only syntax to the
-- compat-130 database.

IF DB_ID('AppDb_Modern') IS NULL CREATE DATABASE AppDb_Modern;
IF DB_ID('AppDb_Legacy') IS NULL CREATE DATABASE AppDb_Legacy;
GO

ALTER DATABASE AppDb_Modern SET COMPATIBILITY_LEVEL = 160;
GO

-- 130 is SchemaSmith's SQL Server compatibility-level floor, so this is the oldest
-- database the tool will deploy to at all.
ALTER DATABASE AppDb_Legacy SET COMPATIBILITY_LEVEL = 130;
GO
