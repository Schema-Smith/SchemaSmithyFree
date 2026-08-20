-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsFunctionalIndex`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsFunctionalIndex`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target supports an index on an expression (a functional/expression key part): the key part
  -- has no COLUMN_NAME and instead reports its text via INFORMATION_SCHEMA.STATISTICS.EXPRESSION, which is
  -- also what SHOW CREATE TABLE renders as (( <expr> )).
  --   * MySQL: since 8.0.13. ServerVersionNum() has major*100+minor granularity (cannot see the .13 patch),
  --     so major >= 8 is treated as yes -- MySQL 8.0.0-8.0.12 is an accepted out-of-scope edge, the same
  --     tradeoff SchemaSmith_SupportsCheckConstraints makes for its 8.0.16 intro.
  --   * MariaDB: no equivalent in this form -- a MariaDB "functional index" is a regular index on a
  --     generated/virtual column, which extraction already reports as a plain column. Always 0.
  RETURN IF(VERSION() LIKE '%MariaDB%', 0, IF(SchemaSmith_ServerVersionNum() >= 800, 1, 0));
END //

DELIMITER ;
