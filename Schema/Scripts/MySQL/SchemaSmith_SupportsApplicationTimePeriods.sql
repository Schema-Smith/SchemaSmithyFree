-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsApplicationTimePeriods`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsApplicationTimePeriods`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target can declare an application-time period -- `PERIOD FOR validity(s, e)` in a
  -- CREATE TABLE, or `ALTER TABLE ... ADD PERIOD FOR`.
  --   * MariaDB: since 10.4.3.
  --   * MySQL: NO equivalent at any version. Application-time periods are a MariaDB feature; this is an
  --     unconditional 0 rather than a threshold MySQL will ever cross.
  --
  -- ServerVersionNum() has major*100+minor granularity and cannot see the .3 patch, so MariaDB
  -- 10.4.0-10.4.2 is an accepted out-of-scope edge -- the same tradeoff, for the same reason, that
  -- SchemaSmith_SupportsColumnSrid documents for MySQL 8.0.0-8.0.2.
  --
  -- NOT the same threshold as READING a period back. INFORMATION_SCHEMA.PERIODS, the only catalog that
  -- reports one, does not arrive until 11.4 -- so between 10.4.3 and 11.3 a period can be DEPLOYED and
  -- never extracted again. That asymmetry is deliberate and documented on MariaDbTable.Periods and in
  -- the reference; it is a property of the engine, not something this gate can close.
  --
  -- A declared period below this degrades: the period clause is suppressed at build time, so the table
  -- deploys without it (one 'downgraded' manifest row is recorded, or under UnsupportedFeaturePolicy
  -- 'fail' the deploy aborts) -- mirrors SchemaSmith_SupportsColumnSrid.
  RETURN IF(VERSION() LIKE '%MariaDB%', IF(SchemaSmith_ServerVersionNum() >= 1004, 1, 0), 0);
END //

DELIMITER ;
