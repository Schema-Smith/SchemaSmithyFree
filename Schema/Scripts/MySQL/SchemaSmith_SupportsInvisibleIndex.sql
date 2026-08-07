-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsInvisibleIndex`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsInvisibleIndex`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target supports hiding an index from the optimizer:
  --   * MySQL: INVISIBLE keyword, since 8.0.0 -> major >= 8 (ServerVersionNum() >= 800).
  --   * MariaDB: IGNORED keyword, since 10.6.0 -> ServerVersionNum() >= 1006.
  -- Below this (MySQL 5.7 / MariaDB 10.2-10.5) the keyword is a hard syntax error (unlike a DESC key part,
  -- which parses-and-ignores). So a declared invisible index degrades: the visibility clause is suppressed
  -- (SchemaSmith_IndexInvisibleClause is only appended when this returns 1), the modified-index compare
  -- ignores the visibility difference so the deploy stays idempotent, and one 'downgraded' manifest row is
  -- recorded (or, under UnsupportedFeaturePolicy 'fail', the deploy aborts).
  RETURN IF((VERSION() LIKE '%MariaDB%' AND SchemaSmith_ServerVersionNum() >= 1006)
            OR (VERSION() NOT LIKE '%MariaDB%' AND SchemaSmith_ServerVersionNum() >= 800), 1, 0);
END //

DELIMITER ;
