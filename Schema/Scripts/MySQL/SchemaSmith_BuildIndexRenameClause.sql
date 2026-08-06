-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_BuildIndexRenameClause`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_BuildIndexRenameClause`(
    p_DatabaseName VARCHAR(128),
    p_TableName VARCHAR(128),
    p_OldIndexName VARCHAR(128),
    p_NewIndexName VARCHAR(128)
)
RETURNS TEXT CHARSET utf8mb4
READS SQL DATA
BEGIN
  -- One ALTER TABLE clause that renames an index. On MySQL 8.0 / MariaDB 10.5.2+ this is the
  -- metadata-only `RENAME INDEX old TO new`. Below that (MariaDB 10.2-10.5, which lack the syntax)
  -- it degrades to `DROP INDEX old, ADD [UNIQUE] INDEX new (cols)` -- a rebuild, but the only route
  -- pre-10.5.2. The recreated index is reconstructed from the OLD index's live catalog metadata
  -- (columns in SEQ_IN_INDEX order, prefix lengths, DESC key parts, uniqueness), so it matches the
  -- existing index exactly; only the name changes. (An index rename is detected only when the old
  -- index already matches the desired columns + uniqueness, so this is a true rename.)
  IF SchemaSmith_SupportsRenameIndex() = 1 THEN
    RETURN CONCAT('RENAME INDEX `', p_OldIndexName, '` TO `', p_NewIndexName, '`');
  END IF;

  RETURN CONCAT('DROP INDEX `', p_OldIndexName, '`, ADD ',
      IF((SELECT MAX(sc.NON_UNIQUE)
          FROM INFORMATION_SCHEMA.STATISTICS sc
          WHERE BINARY sc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY sc.TABLE_NAME = BINARY p_TableName
            AND BINARY sc.INDEX_NAME = BINARY p_OldIndexName) = 0, 'UNIQUE ', ''),
      'INDEX `', p_NewIndexName, '` (',
      CONVERT((
          SELECT GROUP_CONCAT(
                     CONCAT('`', CONVERT(sc.COLUMN_NAME USING utf8mb4), '`',
                            IF(sc.SUB_PART IS NOT NULL, CONCAT('(', sc.SUB_PART, ')'), ''),
                            CASE WHEN BINARY sc.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
                     ORDER BY sc.SEQ_IN_INDEX SEPARATOR ',')
          FROM INFORMATION_SCHEMA.STATISTICS sc
          WHERE BINARY sc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY sc.TABLE_NAME = BINARY p_TableName
            AND BINARY sc.INDEX_NAME = BINARY p_OldIndexName
      ) USING utf8mb4),
      ')');
END //

DELIMITER ;
