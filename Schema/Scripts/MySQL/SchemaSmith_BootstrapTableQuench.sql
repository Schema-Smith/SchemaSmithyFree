-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Lightweight bootstrap procedure with ZERO SchemaSmith_* table or proc dependencies.
-- Parses a TableQuench-shaped JSON definition and applies:
--   1. CREATE TABLE IF NOT EXISTS (built from Columns + any PrimaryKey + inline UNIQUE/INDEX)
--   2. ALTER TABLE ADD COLUMN per missing column (information_schema-guarded)
--   3. ALTER TABLE ADD INDEX per missing non-PK index (information_schema-guarded)
-- Out of scope: column type changes, drops, FKs, check constraints, ownership tracking.
-- Idempotent: a second call on the same definition is a no-op.
-- MySQL note: tables sit in the current DATABASE() (no schema concept); JSON has no "Schema".

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_BootstrapTableQuench//

CREATE PROCEDURE SchemaSmith_BootstrapTableQuench(
    IN p_TableDefinitions LONGTEXT
)
SQL SECURITY INVOKER
BEGIN
    DECLARE v_TableName VARCHAR(128);
    DECLARE v_Db VARCHAR(128);
    DECLARE v_Sql LONGTEXT;
    DECLARE v_ColumnList LONGTEXT;
    DECLARE v_PkClause LONGTEXT;
    DECLARE v_ColumnCount INT;
    DECLARE v_IdxCount INT;
    DECLARE v_Idx INT;
    DECLARE v_ColumnName VARCHAR(128);
    DECLARE v_DataType VARCHAR(200);
    DECLARE v_Nullable TINYINT;
    DECLARE v_Default LONGTEXT;
    DECLARE v_AutoIncrement TINYINT;
    DECLARE v_ColumnPrimaryKey TINYINT;
    DECLARE v_IndexName VARCHAR(128);
    DECLARE v_IndexUnique TINYINT;
    DECLARE v_IndexPrimaryKey TINYINT;
    DECLARE v_IndexColumns LONGTEXT;
    DECLARE v_ColExists INT;
    DECLARE v_IdxExists INT;
    DECLARE v_HasPkColumn INT;

    SET v_TableName = TRIM(BOTH FROM JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, '$.Name')));
    SET v_Db = DATABASE();

    IF v_TableName IS NULL OR v_TableName = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'BootstrapTableQuench: JSON must contain non-blank Name.';
    END IF;

    SET v_ColumnCount = JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, '$.Columns'));
    SET v_IdxCount = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, '$.Indexes')), 0);

    -- Determine if any column declares AutoIncrement + PrimaryKey (legacy MySQL idiom: Id INT AUTO_INCREMENT PRIMARY KEY).
    SET v_HasPkColumn = 0;
    SET v_Idx = 0;
    WHILE v_Idx < v_ColumnCount DO
        SET v_AutoIncrement = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].AutoIncrement')) AS UNSIGNED), 0);
        SET v_ColumnPrimaryKey = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].PrimaryKey')) AS UNSIGNED), 0);
        IF v_AutoIncrement = 1 AND v_ColumnPrimaryKey = 1 THEN
            SET v_HasPkColumn = 1;
        END IF;
        SET v_Idx = v_Idx + 1;
    END WHILE;

    -- Build CREATE TABLE IF NOT EXISTS. Always emit; it's a no-op against existing tables.
    SET v_ColumnList = '';
    SET v_Idx = 0;
    WHILE v_Idx < v_ColumnCount DO
        SET v_ColumnName = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Name')));
        SET v_DataType = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].DataType')));
        SET v_Nullable = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Nullable')) AS UNSIGNED), 0);
        SET v_Default = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Default')));
        SET v_AutoIncrement = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].AutoIncrement')) AS UNSIGNED), 0);
        SET v_ColumnPrimaryKey = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].PrimaryKey')) AS UNSIGNED), 0);

        IF v_ColumnList <> '' THEN
            SET v_ColumnList = CONCAT(v_ColumnList, ', ');
        END IF;
        SET v_ColumnList = CONCAT(v_ColumnList, '`', v_ColumnName, '` ', v_DataType,
            CASE WHEN v_Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END,
            CASE WHEN v_AutoIncrement = 1 THEN ' AUTO_INCREMENT' ELSE '' END,
            CASE WHEN v_Default IS NOT NULL AND TRIM(v_Default) <> '' THEN CONCAT(' DEFAULT ', v_Default) ELSE '' END,
            CASE WHEN v_AutoIncrement = 1 AND v_ColumnPrimaryKey = 1 THEN ' PRIMARY KEY' ELSE '' END);
        SET v_Idx = v_Idx + 1;
    END WHILE;

    -- If a non-column-level PK exists in the indexes array, attach it as a constraint at CREATE TABLE time.
    SET v_PkClause = '';
    IF v_HasPkColumn = 0 THEN
        SET v_Idx = 0;
        WHILE v_Idx < v_IdxCount DO
            SET v_IndexPrimaryKey = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].PrimaryKey')) AS UNSIGNED), 0);
            IF v_IndexPrimaryKey = 1 AND v_PkClause = '' THEN
                SET v_IndexColumns = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].IndexColumns')));
                SET v_PkClause = CONCAT(', PRIMARY KEY (', v_IndexColumns, ')');
            END IF;
            SET v_Idx = v_Idx + 1;
        END WHILE;
    END IF;

    SET v_Sql = CONCAT('CREATE TABLE IF NOT EXISTS `', v_TableName, '` (', v_ColumnList, v_PkClause,
                       ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');
    SET @v_stmt = v_Sql;
    PREPARE stmt FROM @v_stmt;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;

    -- Step 2: ADD COLUMN per missing column on an existing table.
    SET v_Idx = 0;
    WHILE v_Idx < v_ColumnCount DO
        SET v_ColumnName = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Name')));
        SET v_DataType = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].DataType')));
        SET v_Nullable = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Nullable')) AS UNSIGNED), 0);
        SET v_Default = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Default')));
        SET v_AutoIncrement = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].AutoIncrement')) AS UNSIGNED), 0);

        SELECT COUNT(*) INTO v_ColExists
          FROM information_schema.columns
         WHERE table_schema = v_Db
           AND table_name = v_TableName
           AND column_name = v_ColumnName;

        IF v_ColExists = 0 AND v_AutoIncrement = 0 THEN
            -- AUTO_INCREMENT columns are CREATE-TABLE-only; we don't attempt to add them to legacy tables.
            SET v_Sql = CONCAT('ALTER TABLE `', v_TableName, '` ADD COLUMN `', v_ColumnName, '` ', v_DataType,
                CASE WHEN v_Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END,
                CASE WHEN v_Default IS NOT NULL AND TRIM(v_Default) <> '' THEN CONCAT(' DEFAULT ', v_Default) ELSE '' END);
            SET @v_stmt = v_Sql;
            PREPARE stmt FROM @v_stmt;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        END IF;
        SET v_Idx = v_Idx + 1;
    END WHILE;

    -- Step 3: ADD INDEX per missing non-PK index.
    SET v_Idx = 0;
    WHILE v_Idx < v_IdxCount DO
        SET v_IndexPrimaryKey = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].PrimaryKey')) AS UNSIGNED), 0);
        IF v_IndexPrimaryKey = 0 THEN
            SET v_IndexName = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].Name')));
            SET v_IndexUnique = COALESCE(CAST(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].Unique')) AS UNSIGNED), 0);
            SET v_IndexColumns = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].IndexColumns')));

            SELECT COUNT(*) INTO v_IdxExists
              FROM information_schema.statistics
             WHERE table_schema = v_Db
               AND table_name = v_TableName
               AND index_name = v_IndexName;

            IF v_IdxExists = 0 THEN
                SET v_Sql = CONCAT('ALTER TABLE `', v_TableName, '` ADD ',
                    CASE WHEN v_IndexUnique = 1 THEN 'UNIQUE ' ELSE '' END,
                    'INDEX `', v_IndexName, '` (', v_IndexColumns, ')');
                SET @v_stmt = v_Sql;
                PREPARE stmt FROM @v_stmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END IF;
        END IF;
        SET v_Idx = v_Idx + 1;
    END WHILE;
END//

DELIMITER ;
