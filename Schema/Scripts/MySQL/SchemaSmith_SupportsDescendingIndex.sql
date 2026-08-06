-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsDescendingIndex`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsDescendingIndex`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target actually stores DESC index key parts (rather than parsing-and-ignoring them):
  --   * MySQL: since 8.0.0 -> major >= 8.
  --   * MariaDB: since 10.8.0 -> ServerVersionNum() >= 1008.
  -- Below this (MySQL 5.7 / MariaDB 10.2-10.7), a DESC key part is silently stored ascending. There is no
  -- equivalent, so a declared DESC index degrades: it is compared and stored as ascending (see
  -- SchemaSmith_NormalizeIndexColumns, which drops the DESC suffix here so the desired list matches the
  -- ascending catalog and the deploy stays idempotent) and one 'downgraded' manifest row is recorded.
  RETURN IF((VERSION() LIKE '%MariaDB%' AND SchemaSmith_ServerVersionNum() >= 1008)
            OR (VERSION() NOT LIKE '%MariaDB%' AND SchemaSmith_ServerVersionNum() >= 800), 1, 0);
END //

DELIMITER ;
