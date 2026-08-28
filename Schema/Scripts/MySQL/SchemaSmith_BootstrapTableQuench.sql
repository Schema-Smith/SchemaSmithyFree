-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Lightweight bootstrap procedure with ZERO SchemaSmith_* table or proc dependencies.
-- Parses a TableQuench-shaped JSON definition and applies, in order:
--   1. TABLE rename when OldName is set (old table present, new absent) -- BEFORE CREATE TABLE so a
--      renamed table's history is not orphaned under an empty freshly-created new table
--   2. CREATE TABLE IF NOT EXISTS (built from Columns + any PrimaryKey + inline UNIQUE/INDEX)
--   3. COLUMN rename when a column's OldName is set (old column present, new absent) -- BEFORE
--      ADD COLUMN so a renamed column's data is not left behind under an empty new column.
--      RENAME COLUMN on MySQL 8.0 / MariaDB 10.6+; CHANGE COLUMN restating the definition below that.
--   4. ALTER TABLE ADD COLUMN per missing column (information_schema-guarded)
--   5. ALTER TABLE ADD INDEX per missing non-PK index (information_schema-guarded)
-- Both a rename's old AND new name already present (table or column) is a hard failure, not a
-- silent skip -- it means an object exists that the model does not expect.
-- Out of scope: column type changes (beyond a same-shape rename), drops, FKs, check constraints,
-- ownership tracking.
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
    DECLARE v_AcCnt INT;
    DECLARE v_AcIdx INT;
    DECLARE v_AcClauses LONGTEXT;
    DECLARE v_AcColName VARCHAR(128);
    DECLARE v_AcDataType VARCHAR(200);
    DECLARE v_AcNullable TINYINT;
    DECLARE v_AcDefault LONGTEXT;
    DECLARE v_AcAutoIncrement TINYINT;
    DECLARE v_ColExists INT;
    DECLARE v_HasStatusTable INT DEFAULT 0;
    DECLARE v_ClashDetail LONGTEXT;
    DECLARE v_AiCnt INT;
    DECLARE v_AiIdx INT;
    DECLARE v_AiClauses LONGTEXT;
    DECLARE v_AiIndexName VARCHAR(128);
    DECLARE v_AiUnique TINYINT;
    DECLARE v_AiPrimaryKey TINYINT;
    DECLARE v_AiIndexColumns LONGTEXT;
    DECLARE v_IdxExists INT;
    DECLARE v_OldTableName VARCHAR(128);
    DECLARE v_TableRenameOldExists INT;
    DECLARE v_TableRenameNewExists INT;
    DECLARE v_ServerVersionNum INT;
    DECLARE v_SupportsRenameColumn TINYINT;
    DECLARE v_RenIdx INT;
    DECLARE v_RenNewName VARCHAR(128);
    DECLARE v_RenOldName VARCHAR(128);
    DECLARE v_RenOldExists INT;
    DECLARE v_RenNewExists INT;
    DECLARE v_RenDataType VARCHAR(200);
    DECLARE v_RenNullable TINYINT;
    DECLARE v_RenDefault LONGTEXT;

    SET SESSION group_concat_max_len = 1000000;

    SET v_TableName = TRIM(BOTH FROM JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, '$.Name')));
    SET v_Db = DATABASE();

    IF v_TableName IS NULL OR v_TableName = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'BootstrapTableQuench: JSON must contain non-blank Name.';
    END IF;

    -- Step 1: TABLE-level declarative rename (OldName), run BEFORE CREATE TABLE IF NOT EXISTS below --
    -- a freshly-created empty table under the new name would otherwise orphan the old table and every
    -- row of its history. #375's blank/whitespace-OldName trap (SchemaSmith_ParseTableJson.sql) applies
    -- here too: NULLIF(TRIM(...), '') normalizes a blank OldName to "no rename", not a bogus empty name.
    SET v_OldTableName = NULLIF(TRIM(JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, '$.OldName'))), '');
    IF v_OldTableName IS NOT NULL THEN
        SELECT COUNT(*) INTO v_TableRenameOldExists FROM information_schema.tables
         WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_OldTableName;
        SELECT COUNT(*) INTO v_TableRenameNewExists FROM information_schema.tables
         WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_TableName;

        IF v_TableRenameOldExists > 0 AND v_TableRenameNewExists > 0 THEN
            -- MESSAGE_TEXT has a hard 128-char limit; a message that exceeds it is replaced by MySQL's
            -- own "Data too long for condition item 'MESSAGE_TEXT'" error, which tells the operator
            -- nothing about the actual problem. Kept well under the limit deliberately, not trimmed to
            -- exactly fit -- must still contain "already exist" (asserted by the integration tests).
            -- MESSAGE_TEXT cannot carry the object names (128-char cap), so the detail goes to
            -- SchemaSmith_StatusMessages, which reaches the run log. That table is created by this
            -- very proc, so on a fresh database it may not exist yet -- guarded rather than assumed,
            -- since a failing INSERT here would replace a clear refusal with a confusing one.
            SET v_ClashDetail = CONCAT('BootstrapTableQuench: both `', v_Db, '`.`', v_OldTableName,
                                       '` (OldName) and `', v_Db, '`.`', v_TableName,
                                       '` already exist; resolve manually before bootstrap can rename.');
            SELECT COUNT(*) INTO v_HasStatusTable FROM information_schema.tables
             WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY 'SchemaSmith_StatusMessages';
            IF v_HasStatusTable > 0 THEN
                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), v_ClashDetail);
            END IF;
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'BootstrapTableQuench: OldName and current table already exist; rename manually.';
        ELSEIF v_TableRenameOldExists > 0 AND v_TableRenameNewExists = 0 THEN
            SET @v_stmt = CONCAT('RENAME TABLE `', v_Db, '`.`', v_OldTableName, '` TO `', v_Db, '`.`', v_TableName, '`');
            PREPARE stmt FROM @v_stmt;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        END IF;
    END IF;

    SET v_ColumnCount = JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, '$.Columns'));
    SET v_IdxCount = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, '$.Indexes')), 0);

    -- Boolean JSON props are read as `JSON_UNQUOTE(...) IN ('true','1')` rather than
    -- `CAST(... AS UNSIGNED)`: MariaDB's text-based JSON returns the literal 'false', which fails
    -- an integer cast under strict mode. This form yields identical 1/0 on MySQL and MariaDB.
    -- Determine if any column declares AutoIncrement + PrimaryKey (legacy MySQL idiom: Id INT AUTO_INCREMENT PRIMARY KEY).
    SET v_HasPkColumn = 0;
    SET v_Idx = 0;
    WHILE v_Idx < v_ColumnCount DO
        SET v_AutoIncrement = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].AutoIncrement'))) IN ('true','1')), 0);
        SET v_ColumnPrimaryKey = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].PrimaryKey'))) IN ('true','1')), 0);
        IF v_AutoIncrement = 1 AND v_ColumnPrimaryKey = 1 THEN
            SET v_HasPkColumn = 1;
        END IF;
        SET v_Idx = v_Idx + 1;
    END WHILE;

    -- Step 2: Build CREATE TABLE IF NOT EXISTS. Always emit; it's a no-op against existing tables.
    SET v_ColumnList = '';
    SET v_Idx = 0;
    WHILE v_Idx < v_ColumnCount DO
        SET v_ColumnName = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Name')));
        SET v_DataType = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].DataType')));
        SET v_Nullable = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Nullable'))) IN ('true','1')), 0);
        SET v_Default = JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].Default')));
        SET v_AutoIncrement = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].AutoIncrement'))) IN ('true','1')), 0);
        SET v_ColumnPrimaryKey = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_Idx, '].PrimaryKey'))) IN ('true','1')), 0);

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
            SET v_IndexPrimaryKey = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_Idx, '].PrimaryKey'))) IN ('true','1')), 0);
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

    -- Step 3: COLUMN-level declarative rename (OldName), run BEFORE the existing-columns snapshot
    -- below (Step 4 add-missing-columns) so a renamed column's data is not left behind under an
    -- empty freshly-added new column, and so the renamed column is not double-counted as "missing".
    -- Same blank-OldName normalization as the table rename above.
    SET v_ServerVersionNum = COALESCE(@schemasmith_version_override,
        CAST(SUBSTRING_INDEX(VERSION(), '.', 1) AS UNSIGNED) * 100
      + CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(VERSION(), '.', 2), '.', -1) AS UNSIGNED));
    -- Same floor as SchemaSmith_SupportsRenameColumn (MySQL 8.0 / MariaDB 10.6+), reproduced inline:
    -- that function is not kindled until after Bootstrap (see ForgeKindler.GetKindlingScripts), and
    -- Bootstrap may not depend on any SchemaSmith_* helper. @schemasmith_version_override is a plain
    -- session variable (not a SchemaSmith_* object), honored here for the same test affordance the
    -- function itself provides.
    SET v_SupportsRenameColumn = IF((VERSION() LIKE '%MariaDB%' AND v_ServerVersionNum >= 1006)
                                     OR (VERSION() NOT LIKE '%MariaDB%' AND v_ServerVersionNum >= 800), 1, 0);

    SET v_RenIdx = 0;
    WHILE v_RenIdx < v_ColumnCount DO
        SET v_RenNewName = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_RenIdx, '].Name')));
        SET v_RenOldName = NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_RenIdx, '].OldName')))), '');

        IF v_RenOldName IS NOT NULL THEN
            SELECT COUNT(*) INTO v_RenOldExists FROM information_schema.columns
             WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_TableName AND BINARY column_name = BINARY v_RenOldName;
            SELECT COUNT(*) INTO v_RenNewExists FROM information_schema.columns
             WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_TableName AND BINARY column_name = BINARY v_RenNewName;

            IF v_RenOldExists > 0 AND v_RenNewExists > 0 THEN
                -- Same MESSAGE_TEXT 128-char limit as the table-rename SIGNAL above -- kept short with
                -- real headroom, not trimmed to exactly fit.
                -- Same 128-char cap and the same existence guard as the table clash above.
                SET v_ClashDetail = CONCAT('BootstrapTableQuench: both `', v_Db, '`.`', v_TableName, '`.`',
                                           v_RenOldName, '` (OldName) and `', v_RenNewName,
                                           '` already exist; resolve manually before bootstrap can rename.');
                SELECT COUNT(*) INTO v_HasStatusTable FROM information_schema.tables
                 WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY 'SchemaSmith_StatusMessages';
                IF v_HasStatusTable > 0 THEN
                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), v_ClashDetail);
                END IF;
                SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'BootstrapTableQuench: OldName and current column already exist; rename manually.';
            ELSEIF v_RenOldExists > 0 AND v_RenNewExists = 0 THEN
                IF v_SupportsRenameColumn = 1 THEN
                    SET @v_stmt = CONCAT('ALTER TABLE `', v_TableName, '` RENAME COLUMN `', v_RenOldName, '` TO `', v_RenNewName, '`');
                ELSE
                    -- Below MySQL 8.0 / MariaDB 10.6: CHANGE COLUMN restating the column's desired
                    -- definition. Uses the JSON's OWN DataType/Nullable/Default (the same source the
                    -- CREATE TABLE list above used) rather than reverse-engineering the OLD column's
                    -- information_schema definition -- Bootstrap has no later reconciliation pass
                    -- (unlike SchemaSmith_MissingTableAndColumnQuench's CHANGE COLUMN fallback, which
                    -- deliberately omits DEFAULT because ModifiedTableQuench fixes it up later in the
                    -- same deploy). PRIMARY KEY / AUTO_INCREMENT are intentionally not restated here:
                    -- Step 4 below never adds an auto-increment column to an existing table either, so
                    -- a declaratively-renamed column is assumed to be a plain data column.
                    SET v_RenDataType = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_RenIdx, '].DataType')));
                    SET v_RenNullable = COALESCE((JSON_UNQUOTE(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_RenIdx, '].Nullable'))) IN ('true','1')), 0);
                    SET v_RenDefault = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_RenIdx, '].Default')));
                    SET @v_stmt = CONCAT('ALTER TABLE `', v_TableName, '` CHANGE COLUMN `', v_RenOldName, '` `', v_RenNewName, '` ', v_RenDataType,
                        CASE WHEN v_RenNullable = 1 THEN ' NULL' ELSE ' NOT NULL' END,
                        CASE WHEN v_RenDefault IS NOT NULL AND TRIM(v_RenDefault) <> '' THEN CONCAT(' DEFAULT ', v_RenDefault) ELSE '' END);
                END IF;
                PREPARE stmt FROM @v_stmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END IF;
        END IF;
        SET v_RenIdx = v_RenIdx + 1;
    END WHILE;

    -- Step 4: ADD COLUMN per missing column on an existing table.
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

    -- Version-agnostic replacement for a JSON_TABLE('$.Columns[*]') aggregation (JSON_TABLE is
    -- 8.0.4+/10.6+ only): walk the Columns array by index, accumulating one clause per missing,
    -- non-auto-increment column in array order (the loop index stands in for FOR ORDINALITY +
    -- ORDER BY), then emit a single ALTER only if at least one clause was accumulated (mirrors
    -- the original GROUP BY, which produced zero rows when nothing matched).
    SET v_AcCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, '$.Columns')), 0);
    SET v_AcIdx = 0;
    SET v_AcClauses = '';
    WHILE v_AcIdx < v_AcCnt DO
        SET v_AcColName = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_AcIdx, '].Name')));
        SET v_AcDataType = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_AcIdx, '].DataType')));
        SET v_AcNullable = SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_AcIdx, '].Nullable')));
        SET v_AcDefault = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_AcIdx, '].Default')));
        SET v_AcAutoIncrement = SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Columns[', v_AcIdx, '].AutoIncrement')));

        SET v_ColExists = 0;
        SELECT COUNT(*) INTO v_ColExists FROM _SchemaSmith_BootstrapExistingCols ec
        WHERE BINARY ec.ColumnName = BINARY v_AcColName;

        IF COALESCE(v_AcAutoIncrement, 0) = 0 AND v_ColExists = 0 THEN
            SET v_AcClauses = CONCAT(v_AcClauses, IF(v_AcClauses = '', '', ', '),
                'ADD COLUMN `', v_AcColName, '` ', v_AcDataType,
                CASE WHEN v_AcNullable = 1 THEN ' NULL' ELSE ' NOT NULL' END,
                CASE WHEN v_AcDefault IS NOT NULL AND TRIM(v_AcDefault) <> '' THEN CONCAT(' DEFAULT ', v_AcDefault) ELSE '' END);
        END IF;
        SET v_AcIdx = v_AcIdx + 1;
    END WHILE;

    IF v_AcClauses <> '' THEN
        INSERT INTO _SchemaSmith_BootstrapAddColStmts (Stmt)
        VALUES (CONCAT('ALTER TABLE `', v_TableName, '` ', v_AcClauses));
    END IF;

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

    -- Step 5: ADD INDEX per missing non-PK index.
    -- Folded into one ALTER TABLE (all missing indexes as ADD INDEX clauses, in JSON array
    -- order), kept as its own ALTER (not merged with Step 4's) so the add-columns-then-add-
    -- indexes ordering matches the original two-step structure exactly.
    -- Same snapshot-first workaround as Step 4, for the index-existence lookup.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapExistingIdxs;
    CREATE TEMPORARY TABLE _SchemaSmith_BootstrapExistingIdxs (IndexName VARCHAR(128) NOT NULL PRIMARY KEY)
        ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    INSERT IGNORE INTO _SchemaSmith_BootstrapExistingIdxs (IndexName)
    SELECT index_name FROM information_schema.statistics
    WHERE BINARY table_schema = BINARY v_Db AND BINARY table_name = BINARY v_TableName;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_BootstrapAddIdxStmts;
    CREATE TEMPORARY TABLE _SchemaSmith_BootstrapAddIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
        ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Version-agnostic replacement for a JSON_TABLE('$.Indexes[*]') aggregation (JSON_TABLE is
    -- 8.0.4+/10.6+ only): walk the Indexes array by index, accumulating one clause per missing,
    -- non-PK index in array order (the loop index stands in for FOR ORDINALITY + ORDER BY), then
    -- emit a single ALTER only if at least one clause was accumulated (mirrors the original
    -- GROUP BY, which produced zero rows when nothing matched).
    SET v_AiCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, '$.Indexes')), 0);
    SET v_AiIdx = 0;
    SET v_AiClauses = '';
    WHILE v_AiIdx < v_AiCnt DO
        SET v_AiIndexName = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_AiIdx, '].Name')));
        SET v_AiUnique = SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_AiIdx, '].Unique')));
        SET v_AiPrimaryKey = SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_AiIdx, '].PrimaryKey')));
        SET v_AiIndexColumns = SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$.Indexes[', v_AiIdx, '].IndexColumns')));

        SET v_IdxExists = 0;
        SELECT COUNT(*) INTO v_IdxExists FROM _SchemaSmith_BootstrapExistingIdxs ei
        WHERE BINARY ei.IndexName = BINARY v_AiIndexName;

        IF COALESCE(v_AiPrimaryKey, 0) = 0 AND v_IdxExists = 0 THEN
            SET v_AiClauses = CONCAT(v_AiClauses, IF(v_AiClauses = '', '', ', '),
                'ADD ', CASE WHEN v_AiUnique = 1 THEN 'UNIQUE ' ELSE '' END,
                'INDEX `', v_AiIndexName, '` (', v_AiIndexColumns, ')');
        END IF;
        SET v_AiIdx = v_AiIdx + 1;
    END WHILE;

    IF v_AiClauses <> '' THEN
        INSERT INTO _SchemaSmith_BootstrapAddIdxStmts (Stmt)
        VALUES (CONCAT('ALTER TABLE `', v_TableName, '` ', v_AiClauses));
    END IF;

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
