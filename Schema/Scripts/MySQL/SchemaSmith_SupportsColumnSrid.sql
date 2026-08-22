-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_SupportsColumnSrid`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_SupportsColumnSrid`()
RETURNS TINYINT
NOT DETERMINISTIC
NO SQL
BEGIN
  -- 1 when the target supports restricting a spatial column to one spatial reference system via the
  -- column-level SRID attribute (`col POINT SRID 4326`), which also makes
  -- INFORMATION_SCHEMA.COLUMNS.SRS_ID meaningful for that column.
  --   * MySQL: since 8.0.3 (WL#8592) -> major >= 8 (ServerVersionNum() >= 800). ServerVersionNum()
  --     has major*100+minor granularity (cannot see the .3 patch), so MySQL 8.0.0-8.0.2 is an
  --     accepted out-of-scope edge, the same tradeoff SchemaSmith_SupportsInvisibleColumn /
  --     SupportsFunctionalIndex make.
  --   * MariaDB: NO equivalent at any version -- verified live against 11.4.12, the newest supported
  --     release: `col POINT SRID 4326` is a hard syntax error, and INFORMATION_SCHEMA.COLUMNS carries
  --     no SRS_ID column to read even if it did. This is not a version threshold on MariaDB the way
  --     SchemaSmith_SupportsInvisibleColumn's MariaDB branch is -- it is an unconditional 0.
  -- A declared SRID below this degrades: the SRID clause is suppressed at ColumnScript build time
  -- (see ParseTableJson), so the column deploys unrestricted (one 'downgraded' manifest row is
  -- recorded, or under UnsupportedFeaturePolicy 'fail' the deploy aborts) -- mirrors
  -- SchemaSmith_SupportsInvisibleColumn. The live-read side of the divergence (SRS_ID not existing on
  -- MariaDB at all) is isolated in SchemaSmith_ColumnSrid, not here.
  RETURN IF(VERSION() LIKE '%MariaDB%', 0, IF(SchemaSmith_ServerVersionNum() >= 800, 1, 0));
END //

DELIMITER ;
