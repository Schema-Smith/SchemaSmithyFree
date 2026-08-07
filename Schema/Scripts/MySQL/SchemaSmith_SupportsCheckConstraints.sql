-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsCheckConstraints`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsCheckConstraints`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target enforces CHECK constraints AND exposes INFORMATION_SCHEMA.CHECK_CONSTRAINTS:
  --   * MariaDB: enforcement since 10.2.1, the view since 10.2.22 — at/above our 10.2 floor, treat as yes.
  --   * MySQL: both arrived at 8.0.16. ServerVersionNum() has major*100+minor granularity (cannot see the
  --     .16 patch), so major >= 8 is treated as yes; MySQL 8.0.0-8.0.15 is an accepted out-of-scope edge.
  -- Below this (MySQL 5.7), CHECK constraints are parsed-and-ignored and the catalog view does not exist,
  -- so the CHECK catalog reads must be version-gated and CHECK emit must degrade via UnsupportedFeaturePolicy.
  RETURN IF(VERSION() LIKE '%MariaDB%' OR SchemaSmith_ServerVersionNum() >= 800, 1, 0);
END //

DELIMITER ;
