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
    DECLARE v_IndexPrimaryKey TINYINT;
    DECLARE v_IndexColumns LONGTEXT;
    DECLARE v_HasPkColumn INT;

    SET SESSION group_concat_max_len = 1000000;

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
    -- Folded into one ALTER TABLE (all missing columns as ADD COLUMN clauses, in JSON array
    -- order) rather than one ALTER per column. AUTO_INCREMENT columns are CREATE-TABLE-only;
    -- we don't attempt to add them to legacy tables.
    -- Snapshot existing columns into a plain temp table first: a correlated NOT EXISTS against
    -- INFORMATION_SCHEMA inside a JSON_TABLE-sourced query can cache/materialize incorrectly in
    -- MySQL (same optimizer issue documented in SchemaSmith_ParseTableJson.sql).
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapExistingCols;
    CREATE TEMPORARY TABLE _SchemaSmith_BootstrapExistingCols (ColumnName VARCHAR(128) NOT NULL PRIMARY KEY)
        ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    -- BINARY on the INFORMATION_SCHEMA-vs-proc-variable comparisons: on MySQL 8.0 the
    -- INFORMATION_SCHEMA columns collate utf8mb4_0900_ai_ci while proc/temp/JSON strings are
    -- utf8mb4_unicode_ci, and a bare '=' between them throws 1267. Sibling procs
    -- (MissingIndexesAndConstraintsQuench, ParseTableJson) bridge this the same way.
    INSERT INTO _SchemaSmith_BootstrapExistingCols (ColumnName)
    SELECT column_name FROM information_schema.columns
    WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_TableName;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapAddColStmts;
    CREATE TEMPORARY TABLE _SchemaSmith_BootstrapAddColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
        ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO _SchemaSmith_BootstrapAddColStmts (Stmt)
    SELECT CONCAT('ALTER TABLE `', v_TableName, '` ',
                  GROUP_CONCAT(
                      CONCAT('ADD COLUMN `', jc.ColumnName, '` ', jc.DataType,
                          CASE WHEN jc.Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END,
                          CASE WHEN jc.DefaultVal IS NOT NULL AND TRIM(jc.DefaultVal) <> '' THEN CONCAT(' DEFAULT ', jc.DefaultVal) ELSE '' END)
                      ORDER BY jc.ColumnOrdinal SEPARATOR ', '))
    FROM JSON_TABLE(p_TableDefinitions, '$.Columns[*]' COLUMNS (
            ColumnOrdinal FOR ORDINALITY,
            ColumnName VARCHAR(128) PATH '$.Name',
            DataType VARCHAR(200) PATH '$.DataType',
            Nullable TINYINT PATH '$.Nullable',
            DefaultVal LONGTEXT PATH '$.Default',
            AutoIncrement TINYINT PATH '$.AutoIncrement'
         )) AS jc
    WHERE COALESCE(jc.AutoIncrement, 0) = 0
      AND NOT EXISTS (
          SELECT 1 FROM _SchemaSmith_BootstrapExistingCols ec
          WHERE BINARY ec.ColumnName = BINARY jc.ColumnName
      )
    GROUP BY v_TableName;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapExistingCols;

    SET @v_addcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_BootstrapAddColStmts);
    WHILE @v_addcol_id IS NOT NULL DO
        SELECT Stmt INTO @exec_sql FROM _SchemaSmith_BootstrapAddColStmts WHERE RowId = @v_addcol_id;
        PREPARE stmt FROM @exec_sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
        SET @v_addcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_BootstrapAddColStmts WHERE RowId > @v_addcol_id);
    END WHILE;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapAddColStmts;

    -- Step 3: ADD INDEX per missing non-PK index.
    -- Folded into one ALTER TABLE (all missing indexes as ADD INDEX clauses, in JSON array
    -- order), kept as its own ALTER (not merged with Step 2's) so the add-columns-then-add-
    -- indexes ordering matches the original two-step structure exactly.
    -- Same snapshot-first workaround as Step 2, for the index-existence lookup.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapExistingIdxs;
    CREATE TEMPORARY TABLE _SchemaSmith_BootstrapExistingIdxs (IndexName VARCHAR(128) NOT NULL PRIMARY KEY)
        ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    INSERT IGNORE INTO _SchemaSmith_BootstrapExistingIdxs (IndexName)
    SELECT index_name FROM information_schema.statistics
    WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_TableName;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapAddIdxStmts;
    CREATE TEMPORARY TABLE _SchemaSmith_BootstrapAddIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
        ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO _SchemaSmith_BootstrapAddIdxStmts (Stmt)
    SELECT CONCAT('ALTER TABLE `', v_TableName, '` ',
                  GROUP_CONCAT(
                      CONCAT('ADD ', CASE WHEN ji.IndexUnique = 1 THEN 'UNIQUE ' ELSE '' END,
                          'INDEX `', ji.IndexName, '` (', ji.IndexColumns, ')')
                      ORDER BY ji.IdxOrdinal SEPARATOR ', '))
    FROM JSON_TABLE(p_TableDefinitions, '$.Indexes[*]' COLUMNS (
            IdxOrdinal FOR ORDINALITY,
            IndexName VARCHAR(128) PATH '$.Name',
            IndexUnique TINYINT PATH '$.Unique',
            IndexPrimaryKeyFlag TINYINT PATH '$.PrimaryKey',
            IndexColumns LONGTEXT PATH '$.IndexColumns'
         )) AS ji
    WHERE COALESCE(ji.IndexPrimaryKeyFlag, 0) = 0
      AND NOT EXISTS (
          SELECT 1 FROM _SchemaSmith_BootstrapExistingIdxs ei
          WHERE BINARY ei.IndexName = BINARY ji.IndexName
      )
    GROUP BY v_TableName;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapExistingIdxs;

    SET @v_addidx_id := (SELECT MIN(RowId) FROM _SchemaSmith_BootstrapAddIdxStmts);
    WHILE @v_addidx_id IS NOT NULL DO
        SELECT Stmt INTO @exec_sql FROM _SchemaSmith_BootstrapAddIdxStmts WHERE RowId = @v_addidx_id;
        PREPARE stmt FROM @exec_sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
        SET @v_addidx_id := (SELECT MIN(RowId) FROM _SchemaSmith_BootstrapAddIdxStmts WHERE RowId > @v_addidx_id);
    END WHILE;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapAddIdxStmts;
END//

DELIMITER ;
