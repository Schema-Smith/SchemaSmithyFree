-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsSystemVersioning`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsSystemVersioning`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target understands system versioning, and therefore the per-column
  -- `WITHOUT SYSTEM VERSIONING` clause that excludes a column from a versioned table's row history.
  --   * MariaDB: since 10.3.4.
  --   * MySQL: NO equivalent at any version. System versioning is a MariaDB feature, so this is an
  --     unconditional 0 rather than a threshold MySQL will ever cross -- the same shape as
  --     SchemaSmith_SupportsApplicationTimePeriods.
  --
  -- ServerVersionNum() has major*100+minor granularity and cannot see the .4 patch, so MariaDB
  -- 10.3.0-10.3.3 is an accepted out-of-scope edge -- the tradeoff SchemaSmith_SupportsColumnSrid
  -- documents for MySQL 8.0.0-8.0.2 and SupportsApplicationTimePeriods for 10.4.0-10.4.2.
  --
  -- Below this the clause is suppressed at build time, so the column deploys ordinarily rather than
  -- failing the whole statement on syntax the engine cannot parse.
  RETURN IF(VERSION() LIKE '%MariaDB%', IF(SchemaSmith_ServerVersionNum() >= 1003, 1, 0), 0);
END //

DELIMITER ;
