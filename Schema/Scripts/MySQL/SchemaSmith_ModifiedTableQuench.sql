-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_ModifiedTableQuench//

CREATE PROCEDURE SchemaSmith_ModifiedTableQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropTablesRemovedFromProduct TINYINT,
    IN p_DropColumnsRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure modifies existing tables to match the JSON definitions.
    -- It reads from the _SchemaSmith_Tables and _SchemaSmith_Columns temp tables.
    --
    -- Execution order:
    -- 0. Ownership validation (error if table owned by another product)
    -- 1. Table renames (when OldName is set and old table exists)
    -- 2. Column renames (when OldName is set and old column exists)
    -- 3. Column modifications (type, nullable, etc.)
    -- 4. Engine changes
    -- 5. Collation changes
    -- 6. Row format changes
    -- 7. Auto-increment seed changes
    -- 8. ProductOwnership updates
    -- 9. Drop tables removed from product (if enabled)

    DECLARE v_ConflictingTable VARCHAR(128);
    DECLARE v_ConflictingOwner VARCHAR(100);

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN ModifiedTableQuench');

    -- =======================
    -- STEP 0: OWNERSHIP VALIDATION
    -- =======================
    -- Check if any tables in the definition are owned by a different product
    SELECT po.ObjectName, po.ProductName
    INTO v_ConflictingTable, v_ConflictingOwner
    FROM _SchemaSmith_Tables t
    INNER JOIN SchemaSmith_ProductOwnership po
        ON CONVERT(po.ObjectName USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4)
        AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
        AND po.ObjectType = 'TABLE'
    WHERE CONVERT(po.ProductName USING utf8mb4) != CONVERT(p_ProductName USING utf8mb4)
    LIMIT 1;

    IF v_ConflictingTable IS NOT NULL THEN
        -- Use user variable for dynamic message (SIGNAL requires constant or user variable)
        SET @schemasmith_error_msg = CONCAT('Table ', v_ConflictingTable, ' is already owned by another product: ', v_ConflictingOwner);
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = @schemasmith_error_msg;
    END IF;

    -- =======================
    -- STEP 1: TABLE RENAMES
    -- =======================
    -- Rename tables where OldName is specified and old table exists
    -- Note: Using BINARY for comparisons to avoid collation issues between
    -- INFORMATION_SCHEMA (utf8mb3), SchemaSmith functions (utf8mb4_unicode_ci),
    -- and connection parameters (utf8mb4_0900_ai_ci)
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle table renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('RENAME TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(t.OldName), '` TO `',
                      p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(t.TableName), '`')
        FROM _SchemaSmith_Tables t
        WHERE t.OldName IS NOT NULL
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
          );
    ELSE
        BEGIN
            DECLARE v_TableRenameDone INT DEFAULT FALSE;
            DECLARE v_TableRenameSql TEXT;
            DECLARE v_OldTableName VARCHAR(128);
            DECLARE v_NewTableName VARCHAR(128);
            DECLARE cur_TableRenames CURSOR FOR
                SELECT
                    SchemaSmith_StripBacktickWrapping(t.OldName) AS OldTableName,
                    SchemaSmith_StripBacktickWrapping(t.TableName) AS NewTableName,
                    CONCAT('RENAME TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                           SchemaSmith_StripBacktickWrapping(t.OldName), '` TO `',
                           p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(t.TableName), '`') AS RenameSql
                FROM _SchemaSmith_Tables t
                WHERE t.OldName IS NOT NULL
                  AND t.NewTable = 0
                  AND EXISTS (
                      SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                      WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                        AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.OldName)
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                      WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                        AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                  );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_TableRenameDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle table renames');
            SET v_TableRenameDone = FALSE;
            OPEN cur_TableRenames;

            table_rename_loop: LOOP
                FETCH cur_TableRenames INTO v_OldTableName, v_NewTableName, v_TableRenameSql;
                IF v_TableRenameDone THEN
                    LEAVE table_rename_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Rename table `', v_OldTableName, '` to `', v_NewTableName, '`'));
                SET @exec_sql = v_TableRenameSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                -- Update ProductOwnership for the renamed table
                UPDATE SchemaSmith_ProductOwnership
                SET ObjectName = v_NewTableName
                WHERE ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
                  AND ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                  AND ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'TABLE' COLLATE utf8mb4_unicode_ci
                  AND ObjectName COLLATE utf8mb4_unicode_ci = v_OldTableName COLLATE utf8mb4_unicode_ci;
            END LOOP;

            CLOSE cur_TableRenames;
        END;
    END IF;

    -- =======================
    -- STEP 2: COLUMN RENAMES
    -- =======================
    -- Rename columns where OldName is specified and old column exists
    -- Uses MySQL 8.0.14+ RENAME COLUMN syntax
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle column renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' RENAME COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                      '` TO `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`')
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.OldName IS NOT NULL
          AND c.NewColumn = 0
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
          );
    ELSE
        BEGIN
            DECLARE v_ColRenameDone INT DEFAULT FALSE;
            DECLARE v_ColRenameSql TEXT;
            DECLARE cur_ColumnRenames CURSOR FOR
                SELECT
                    CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                           ' RENAME COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                           '` TO `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`') AS RenameSql
                FROM _SchemaSmith_Columns c
                INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                WHERE c.OldName IS NOT NULL
                  AND c.NewColumn = 0
                  AND t.NewTable = 0
                  AND EXISTS (
                      SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
                      WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                        AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                        AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName)
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
                      WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                        AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                        AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
                  );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_ColRenameDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle column renames');
            SET v_ColRenameDone = FALSE;
            OPEN cur_ColumnRenames;

            col_rename_loop: LOOP
                FETCH cur_ColumnRenames INTO v_ColRenameSql;
                IF v_ColRenameDone THEN
                    LEAVE col_rename_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Rename column: ', v_ColRenameSql));
                SET @exec_sql = v_ColRenameSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_ColumnRenames;
        END;
    END IF;

    -- =======================
    -- STEP 3: COLUMN MODIFICATIONS (excludes generated status changes)
    -- =======================
    -- Output column modifications
    -- NOTE: Excludes columns that are changing their generated status since MySQL
    -- doesn't support that via ALTER MODIFY - those are handled in Step 3.5
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Modify columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' MODIFY COLUMN ', c.ColumnScript)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          -- Exclude columns where generated status is changing (regular->generated or generated->regular)
          AND NOT (
              -- Currently generated, wants to be regular
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              -- Currently regular, wants to be generated
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
          AND (
              -- Normalize whitespace adjacent to structural delimiters and the DECIMAL/NUMERIC
              -- synonym so a hand-authored type that differs only by spacing/synonym is not a
              -- false "modified". Guarded for ENUM/SET, whose parenthesized content is string
              -- values where whitespace is meaningful (normalizing could miss a real change).
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN UPPER(isc.COLUMN_TYPE) != UPPER(c.DataType)
                   ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                     != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              -- Default value changes (strip outer single quotes from JSON default for comparison,
              -- since GenerateTableJson wraps string/enum defaults in quotes for DDL but INFORMATION_SCHEMA stores raw values)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (isc.COLUMN_DEFAULT IS NULL OR BINARY isc.COLUMN_DEFAULT != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END))
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND isc.COLUMN_DEFAULT IS NOT NULL)
              -- Collation changes (only when JSON specifies a collation)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              -- Generated expression changes (both sides are generated, but expression differs)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              -- AUTO_INCREMENT removal/addition (live EXTRA vs declared IsAutoIncrement) — parity with identity removal
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
          );
    ELSE
        BEGIN
            DECLARE v_ModifyDone INT DEFAULT FALSE;
            DECLARE v_ModifySql TEXT;
            DECLARE cur_ModifyColumns CURSOR FOR
                SELECT
                    CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                           ' MODIFY COLUMN ', c.ColumnScript) AS AlterColumnStatement
                FROM _SchemaSmith_Columns c
                INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
                    ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                    AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
                WHERE t.NewTable = 0
                  AND c.NewColumn = 0
                  -- Exclude columns where generated status is changing (regular->generated or generated->regular)
                  AND NOT (
                      -- Currently generated, wants to be regular
                      ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
                       AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
                      OR
                      -- Currently regular, wants to be generated
                      ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
                       AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
                  )
                  AND (
                      -- Normalize whitespace adjacent to structural delimiters and the DECIMAL/NUMERIC
                      -- synonym so a hand-authored type that differs only by spacing/synonym is not a
                      -- false "modified". Guarded for ENUM/SET, whose parenthesized content is string
                      -- values where whitespace is meaningful (normalizing could miss a real change).
                      CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                             OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                           THEN UPPER(isc.COLUMN_TYPE) != UPPER(c.DataType)
                           ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                             != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') END
                      OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
                      OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
                      -- Default value changes (strip outer single quotes from JSON default for comparison,
                      -- since GenerateTableJson wraps string/enum defaults in quotes for DDL but INFORMATION_SCHEMA stores raw values)
                      OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                          AND (isc.COLUMN_DEFAULT IS NULL OR BINARY isc.COLUMN_DEFAULT != BINARY
                              CASE WHEN c.DefaultValue LIKE '''%'''
                                   THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                                   ELSE c.DefaultValue END))
                      OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND isc.COLUMN_DEFAULT IS NOT NULL)
                      -- Collation changes (only when JSON specifies a collation)
                      OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
                      -- Generated expression changes (both sides are generated, but expression differs)
                      OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                          AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
                      -- AUTO_INCREMENT removal/addition (live EXTRA vs declared IsAutoIncrement) — parity with identity removal
                      OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
                  );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_ModifyDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Modify columns');
            SET v_ModifyDone = FALSE;
            OPEN cur_ModifyColumns;

            modify_columns_loop: LOOP
                FETCH cur_ModifyColumns INTO v_ModifySql;
                IF v_ModifyDone THEN
                    LEAVE modify_columns_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Modify column: ', v_ModifySql));
                SET @exec_sql = v_ModifySql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_ModifyColumns;
        END;
    END IF;

    -- =======================
    -- STEP 3.5: GENERATED STATUS CHANGES (via DROP+ADD)
    -- =======================
    -- MySQL doesn't support changing a column's generated status via ALTER MODIFY
    -- So we must DROP the column and ADD it with the new definition
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Recreate columns (generated status changes)');
        -- Show DROP COLUMN statements
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(c.TableName), '` DROP COLUMN `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`')
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          );
        -- Show ADD COLUMN statements
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(c.TableName), '` ADD COLUMN ', c.ColumnScript)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          );
    ELSE
        BEGIN
            DECLARE v_GenChangeDone INT DEFAULT FALSE;
            DECLARE v_GenChangeTableName VARCHAR(128);
            DECLARE v_GenChangeColName VARCHAR(128);
            DECLARE v_GenChangeColScript TEXT;
            DECLARE cur_GenStatusChanges CURSOR FOR
                SELECT
                    SchemaSmith_StripBacktickWrapping(c.TableName) AS TableName,
                    SchemaSmith_StripBacktickWrapping(c.ColumnName) AS ColName,
                    c.ColumnScript
                FROM _SchemaSmith_Columns c
                INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
                    ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                    AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
                WHERE t.NewTable = 0
                  AND c.NewColumn = 0
                  AND (
                      -- Currently generated, wants to be regular
                      ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
                       AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
                      OR
                      -- Currently regular, wants to be generated
                      ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
                       AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
                  );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_GenChangeDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Recreate columns (generated status changes)');
            SET v_GenChangeDone = FALSE;
            OPEN cur_GenStatusChanges;

            gen_status_loop: LOOP
                FETCH cur_GenStatusChanges INTO v_GenChangeTableName, v_GenChangeColName, v_GenChangeColScript;
                IF v_GenChangeDone THEN
                    LEAVE gen_status_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Recreate column: ', v_GenChangeTableName, '.', v_GenChangeColName));
                -- First DROP the column
                SET @exec_sql = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', v_GenChangeTableName, '` DROP COLUMN `', v_GenChangeColName, '`');
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                -- Then ADD it back with the new definition
                SET @exec_sql = CONCAT('ALTER TABLE `', p_DatabaseName, '`.`', v_GenChangeTableName, '` ADD COLUMN ', v_GenChangeColScript);
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_GenStatusChanges;
        END;
    END IF;

    -- =======================
    -- STEP 4: DROP UNUSED COLUMNS
    -- =======================
    -- Drop columns that exist in the database but are not in the JSON definition
    -- Must drop dependencies first (FKs, check constraints, indexes, generated columns)
    --
    -- Note: We use helper temp tables to avoid MySQL's "Can't reopen table" error
    -- (Error 1137) which occurs when a temp table is referenced multiple times
    -- in the same query (including in subqueries).

    -- Create helper table to copy defined columns (avoids referencing _SchemaSmith_Columns multiple times)
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedColumns;
    CREATE TEMPORARY TABLE _SchemaSmith_DefinedColumns (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        OldName VARCHAR(128) NULL,
        INDEX idx_table_col (TableName, ColumnName),
        INDEX idx_table_old (TableName, OldName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO _SchemaSmith_DefinedColumns (TableName, ColumnName, OldName)
    SELECT
        SchemaSmith_StripBacktickWrapping(c.TableName),
        SchemaSmith_StripBacktickWrapping(c.ColumnName),
        CASE WHEN c.OldName IS NOT NULL THEN SchemaSmith_StripBacktickWrapping(c.OldName) ELSE NULL END
    FROM _SchemaSmith_Columns c;

    -- Create helper table for columns to drop (used by all subsequent cursors)
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColumnsToDrop;
    CREATE TEMPORARY TABLE _SchemaSmith_ColumnsToDrop (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Identify columns to drop: exist in DB but not in JSON definition
    INSERT INTO _SchemaSmith_ColumnsToDrop (TableName, ColumnName)
    SELECT
        SchemaSmith_StripBacktickWrapping(t.TableName) AS TableName,
        isc.COLUMN_NAME AS ColumnName
    FROM _SchemaSmith_Tables t
    INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
        ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
    LEFT JOIN _SchemaSmith_DefinedColumns dc
        ON dc.TableName = SchemaSmith_StripBacktickWrapping(t.TableName)
        AND (
            BINARY dc.ColumnName = BINARY isc.COLUMN_NAME
            OR (dc.OldName IS NOT NULL AND BINARY dc.OldName = BINARY isc.COLUMN_NAME)
        )
    WHERE t.NewTable = 0
      AND dc.ColumnName IS NULL
      AND p_DropColumnsRemovedFromProduct = 1
      AND COALESCE(t.DropColumnsRemovedFromProduct, 1) = 1;

    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unused columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` DROP COLUMN `', ColumnName, '`')
        FROM _SchemaSmith_ColumnsToDrop;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unused columns');
        -- First, drop foreign keys that reference columns we're about to drop
        -- Note: Uses a helper table with two separate INSERTs to avoid MySQL optimizer bug
        -- where an OR in a JOIN against INFORMATION_SCHEMA.KEY_COLUMN_USAGE with multiple
        -- temp table rows produces incorrect (empty) results.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_FKsToDrop (
            DropFKSql TEXT NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Branch 1: FK source columns being dropped
        INSERT INTO _SchemaSmith_FKsToDrop (DropFKSql)
        SELECT DISTINCT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                               CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                               '` DROP FOREIGN KEY `', CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
            ON CONVERT(kcu.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(kcu.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(kcu.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
            AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
            AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
            AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY';

        -- Branch 2: FK referenced (target) columns being dropped
        INSERT IGNORE INTO _SchemaSmith_FKsToDrop (DropFKSql)
        SELECT DISTINCT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                               CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                               '` DROP FOREIGN KEY `', CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
            ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(kcu.REFERENCED_COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
            AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
            AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
            AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY';

        BEGIN
            DECLARE v_DropFKDone INT DEFAULT FALSE;
            DECLARE v_DropFKSql TEXT;
            DECLARE cur_DropFKsForColumns CURSOR FOR
                SELECT DropFKSql FROM _SchemaSmith_FKsToDrop;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropFKDone = TRUE;

            SET v_DropFKDone = FALSE;
            OPEN cur_DropFKsForColumns;

            drop_fks_for_cols_loop: LOOP
                FETCH cur_DropFKsForColumns INTO v_DropFKSql;
                IF v_DropFKDone THEN
                    LEAVE drop_fks_for_cols_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop FK for column: ', v_DropFKSql));
                SET @exec_sql = v_DropFKSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_DropFKsForColumns;
        END;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsToDrop;

        -- Drop check constraints that reference columns being dropped
        BEGIN
            DECLARE v_DropCKDone INT DEFAULT FALSE;
            DECLARE v_DropCKSql TEXT;
            DECLARE cur_DropCKsForColumns CURSOR FOR
                SELECT DISTINCT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                                       CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                       '` DROP CONSTRAINT `', CONVERT(cc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`') AS DropCKSql
                FROM _SchemaSmith_ColumnsToDrop ctd
                INNER JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
                    ON CONVERT(cc.CONSTRAINT_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    -- Check if constraint references this column (explicit COLLATE to avoid collation mismatch)
                    AND CONVERT(cc.CHECK_CLAUSE USING utf8mb4) COLLATE utf8mb4_unicode_ci
                        LIKE CONCAT('%`', ctd.ColumnName COLLATE utf8mb4_unicode_ci, '`%')
                INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    ON CONVERT(tc.CONSTRAINT_SCHEMA USING utf8mb4) = CONVERT(cc.CONSTRAINT_SCHEMA USING utf8mb4)
                    AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(cc.CONSTRAINT_NAME USING utf8mb4)
                    AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
                    AND tc.CONSTRAINT_TYPE = 'CHECK';

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropCKDone = TRUE;

            SET v_DropCKDone = FALSE;
            OPEN cur_DropCKsForColumns;

            drop_cks_for_cols_loop: LOOP
                FETCH cur_DropCKsForColumns INTO v_DropCKSql;
                IF v_DropCKDone THEN
                    LEAVE drop_cks_for_cols_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop check constraint for column: ', v_DropCKSql));
                SET @exec_sql = v_DropCKSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_DropCKsForColumns;
        END;

        -- Drop indexes that use columns being dropped
        BEGIN
            DECLARE v_DropIdxDone INT DEFAULT FALSE;
            DECLARE v_DropIdxSql TEXT;
            DECLARE cur_DropIdxsForColumns CURSOR FOR
                SELECT DISTINCT CONCAT('DROP INDEX `', CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                       '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                                       CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`') AS DropIdxSql
                FROM _SchemaSmith_ColumnsToDrop ctd
                INNER JOIN INFORMATION_SCHEMA.STATISTICS s
                    ON CONVERT(s.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(s.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
                    AND CONVERT(s.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
                WHERE UPPER(s.INDEX_NAME) != 'PRIMARY';

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropIdxDone = TRUE;

            SET v_DropIdxDone = FALSE;
            OPEN cur_DropIdxsForColumns;

            drop_idxs_for_cols_loop: LOOP
                FETCH cur_DropIdxsForColumns INTO v_DropIdxSql;
                IF v_DropIdxDone THEN
                    LEAVE drop_idxs_for_cols_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop index for column: ', v_DropIdxSql));
                SET @exec_sql = v_DropIdxSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_DropIdxsForColumns;
        END;

        -- Drop generated columns that reference columns being dropped
        BEGIN
            DECLARE v_DropGenDone INT DEFAULT FALSE;
            DECLARE v_DropGenSql TEXT;
            DECLARE cur_DropGenCols CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                              CONVERT(isc_gen.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                              '` DROP COLUMN `', CONVERT(isc_gen.COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`') AS DropGenSql
                FROM _SchemaSmith_ColumnsToDrop ctd
                INNER JOIN INFORMATION_SCHEMA.COLUMNS isc_gen
                    ON CONVERT(isc_gen.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(isc_gen.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
                    AND isc_gen.GENERATION_EXPRESSION IS NOT NULL
                    AND isc_gen.GENERATION_EXPRESSION != ''
                    -- Explicit COLLATE to avoid collation mismatch between INFORMATION_SCHEMA and temp table
                    AND CONVERT(isc_gen.GENERATION_EXPRESSION USING utf8mb4) COLLATE utf8mb4_unicode_ci
                        LIKE CONCAT('%`', ctd.ColumnName COLLATE utf8mb4_unicode_ci, '`%')
                -- Only drop generated columns that are also not in the definition
                -- (use _SchemaSmith_DefinedColumns to avoid MySQL's "Can't reopen table" error)
                LEFT JOIN _SchemaSmith_DefinedColumns dc_gen
                    ON dc_gen.TableName = ctd.TableName
                    AND BINARY dc_gen.ColumnName = BINARY isc_gen.COLUMN_NAME
                WHERE dc_gen.ColumnName IS NULL;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropGenDone = TRUE;

            SET v_DropGenDone = FALSE;
            OPEN cur_DropGenCols;

            drop_gen_cols_loop: LOOP
                FETCH cur_DropGenCols INTO v_DropGenSql;
                IF v_DropGenDone THEN
                    LEAVE drop_gen_cols_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop generated column: ', v_DropGenSql));
                SET @exec_sql = v_DropGenSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_DropGenCols;
        END;

        -- Now drop the columns themselves
        BEGIN
            DECLARE v_DropColDone2 INT DEFAULT FALSE;
            DECLARE v_DropColSql2 TEXT;
            DECLARE cur_DropColumns CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                              CONVERT(ctd.TableName USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                              '` DROP COLUMN `', CONVERT(ctd.ColumnName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`') AS DropColSql
                FROM _SchemaSmith_ColumnsToDrop ctd
                INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
                    ON CONVERT(isc.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(isc.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
                    AND CONVERT(isc.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
                -- Not a generated column (those were handled above)
                WHERE (isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '');

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropColDone2 = TRUE;

            SET v_DropColDone2 = FALSE;
            OPEN cur_DropColumns;

            drop_cols_loop: LOOP
                FETCH cur_DropColumns INTO v_DropColSql2;
                IF v_DropColDone2 THEN
                    LEAVE drop_cols_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop column: ', v_DropColSql2));
                SET @exec_sql = v_DropColSql2;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_DropColumns;
        END;
    END IF;

    -- Clean up helper tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColumnsToDrop;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedColumns;

    -- =======================
    -- STEP 5: ALTER TABLE ENGINE
    -- =======================
    -- Alter table engine if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table engine');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ENGINE = ', t.Engine)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.Engine IS NOT NULL
          AND UPPER(ist.ENGINE) != UPPER(t.Engine);
    ELSE
        BEGIN
            DECLARE v_EngineDone INT DEFAULT FALSE;
            DECLARE v_EngineSql TEXT;
            DECLARE cur_EngineChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ENGINE = ', t.Engine) AS AlterEngineStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.Engine IS NOT NULL
                  AND UPPER(ist.ENGINE) != UPPER(t.Engine);

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_EngineDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table engine');
            SET v_EngineDone = FALSE;
            OPEN cur_EngineChanges;

            engine_changes_loop: LOOP
                FETCH cur_EngineChanges INTO v_EngineSql;
                IF v_EngineDone THEN
                    LEAVE engine_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change engine: ', v_EngineSql));
                SET @exec_sql = v_EngineSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_EngineChanges;
        END;
    END IF;

    -- Alter table collation if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table collation');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
                      ' CONVERT TO CHARACTER SET ',
                      SUBSTRING_INDEX(t.Collation, '_', 1),
                      ' COLLATE ', t.Collation)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.Collation IS NOT NULL
          AND ist.TABLE_COLLATION != t.Collation;
    ELSE
        BEGIN
            DECLARE v_CollationDone INT DEFAULT FALSE;
            DECLARE v_CollationSql TEXT;
            DECLARE cur_CollationChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
                              ' CONVERT TO CHARACTER SET ',
                              SUBSTRING_INDEX(t.Collation, '_', 1),
                              ' COLLATE ', t.Collation) AS AlterCollationStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.Collation IS NOT NULL
                  AND ist.TABLE_COLLATION != t.Collation;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_CollationDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table collation');
            SET v_CollationDone = FALSE;
            OPEN cur_CollationChanges;

            collation_changes_loop: LOOP
                FETCH cur_CollationChanges INTO v_CollationSql;
                IF v_CollationDone THEN
                    LEAVE collation_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change collation: ', v_CollationSql));
                SET @exec_sql = v_CollationSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_CollationChanges;
        END;
    END IF;

    -- =======================
    -- STEP 6: ALTER TABLE ROW_FORMAT
    -- =======================
    -- Alter table row format if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table row format');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ROW_FORMAT=', t.RowFormat)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.RowFormat IS NOT NULL
          AND t.RowFormat != ''
          AND UPPER(ist.ROW_FORMAT) != UPPER(t.RowFormat);
    ELSE
        BEGIN
            DECLARE v_RowFormatDone INT DEFAULT FALSE;
            DECLARE v_RowFormatSql TEXT;
            DECLARE cur_RowFormatChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ROW_FORMAT=', t.RowFormat) AS AlterRowFormatStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.RowFormat IS NOT NULL
                  AND t.RowFormat != ''
                  AND UPPER(ist.ROW_FORMAT) != UPPER(t.RowFormat);

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_RowFormatDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table row format');
            SET v_RowFormatDone = FALSE;
            OPEN cur_RowFormatChanges;

            rowformat_changes_loop: LOOP
                FETCH cur_RowFormatChanges INTO v_RowFormatSql;
                IF v_RowFormatDone THEN
                    LEAVE rowformat_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change row format: ', v_RowFormatSql));
                SET @exec_sql = v_RowFormatSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_RowFormatChanges;
        END;
    END IF;

    -- =======================
    -- STEP 7: ALTER TABLE AUTO_INCREMENT
    -- =======================
    -- Set auto-increment seed when declared value is higher than the live value (set-if-higher, idempotent).
    -- COALESCE(ist.AUTO_INCREMENT, 0): MySQL returns NULL for AUTO_INCREMENT on an empty InnoDB table,
    -- treating NULL as 0 ensures a declared seed is applied even on tables that have never had rows.
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Set auto-increment seed');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' AUTO_INCREMENT=', t.AutoIncrementValue)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.AutoIncrementValue IS NOT NULL
          AND t.AutoIncrementValue > COALESCE(ist.AUTO_INCREMENT, 0);
    ELSE
        BEGIN
            DECLARE v_AutoIncDone INT DEFAULT FALSE;
            DECLARE v_AutoIncSql TEXT;
            DECLARE cur_AutoIncChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' AUTO_INCREMENT=', t.AutoIncrementValue) AS AlterAutoIncStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.AutoIncrementValue IS NOT NULL
                  AND t.AutoIncrementValue > COALESCE(ist.AUTO_INCREMENT, 0);

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_AutoIncDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Set auto-increment seed');
            SET v_AutoIncDone = FALSE;
            OPEN cur_AutoIncChanges;

            autoinc_changes_loop: LOOP
                FETCH cur_AutoIncChanges INTO v_AutoIncSql;
                IF v_AutoIncDone THEN
                    LEAVE autoinc_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Set auto-increment: ', v_AutoIncSql));
                SET @exec_sql = v_AutoIncSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_AutoIncChanges;
        END;
    END IF;

    -- Update ProductOwnership for managed tables (non-WhatIf mode only)
    IF p_WhatIf = 0 THEN
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'TABLE', SchemaSmith_StripBacktickWrapping(t.TableName)
        FROM _SchemaSmith_Tables t
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
            WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        );
    END IF;

    -- =======================
    -- STEP 8: DROP TABLES REMOVED FROM PRODUCT
    -- =======================
    -- Drop tables that are owned by this product but no longer in the definition
    IF p_DropTablesRemovedFromProduct = 1 THEN
        -- #289: drop any foreign key that REFERENCES a table about to be removed BEFORE the table
        -- drop below, otherwise the DROP TABLE fails on a still-present inbound dependency.
        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop inbound foreign keys referencing tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT DISTINCT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                                   CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                   '` DROP FOREIGN KEY `', CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
            FROM SchemaSmith_ProductOwnership po
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
               AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
               AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
               AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
               AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );
        ELSE
            -- Helper temp table + cursor mirrors the column-drop FK pattern above (avoids the MySQL
            -- optimizer bug with INFORMATION_SCHEMA.KEY_COLUMN_USAGE joins and keeps DECLARE ordering).
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKsToDrop;
            CREATE TEMPORARY TABLE _SchemaSmith_InboundFKsToDrop (
                DropFKSql TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            INSERT INTO _SchemaSmith_InboundFKsToDrop (DropFKSql)
            SELECT DISTINCT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                                   CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                   '` DROP FOREIGN KEY `', CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
            FROM SchemaSmith_ProductOwnership po
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
               AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
               AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
               AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
               AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );

            BEGIN
                DECLARE v_DropInFKDone INT DEFAULT FALSE;
                DECLARE v_DropInFKSql TEXT;
                DECLARE cur_DropInboundFKs CURSOR FOR
                    SELECT DropFKSql FROM _SchemaSmith_InboundFKsToDrop;

                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropInFKDone = TRUE;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop inbound foreign keys referencing tables removed from product');
                SET v_DropInFKDone = FALSE;
                OPEN cur_DropInboundFKs;

                drop_inbound_fks_loop: LOOP
                    FETCH cur_DropInboundFKs INTO v_DropInFKSql;
                    IF v_DropInFKDone THEN
                        LEAVE drop_inbound_fks_loop;
                    END IF;

                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop inbound FK: ', v_DropInFKSql));
                    SET @exec_sql = v_DropInFKSql;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                END LOOP;

                CLOSE cur_DropInboundFKs;
            END;

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKsToDrop;
        END IF;

        -- A CustomTableDrop hook (e.g. a recyclebin pattern) replaces the plain DROP TABLE when the
        -- user has installed a SchemaSmith_CustomTableDrop procedure in this database, mirroring the
        -- SQL Server / PostgreSQL hook.
        SET @has_custom_drop = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES
                                WHERE CONVERT(ROUTINE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                                  AND ROUTINE_NAME = 'SchemaSmith_CustomTableDrop'
                                  AND ROUTINE_TYPE = 'PROCEDURE');

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(),
                   CASE WHEN @has_custom_drop = 1
                        THEN CONCAT('CALL SchemaSmith_CustomTableDrop(''', p_DatabaseName COLLATE utf8mb4_unicode_ci, ''', ''', po.ObjectName, ''')')
                        ELSE CONCAT('DROP TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', po.ObjectName, '`')
                        END
            FROM SchemaSmith_ProductOwnership po
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              -- Table exists
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              -- Not in current definition
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );
        ELSE
            BEGIN
                DECLARE v_DropTableDone INT DEFAULT FALSE;
                DECLARE v_DropTableSql TEXT;
                DECLARE v_DroppedTableName VARCHAR(128);
                DECLARE cur_DropTables CURSOR FOR
                    SELECT
                        po.ObjectName AS TableName,
                        CASE WHEN @has_custom_drop = 1
                             THEN CONCAT('CALL SchemaSmith_CustomTableDrop(''', p_DatabaseName COLLATE utf8mb4_unicode_ci, ''', ''', po.ObjectName, ''')')
                             ELSE CONCAT('DROP TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', po.ObjectName, '`')
                             END AS DropSql
                    FROM SchemaSmith_ProductOwnership po
                    WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
                      AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                      AND po.ObjectType = 'TABLE'
                      -- Table exists
                      AND EXISTS (
                          SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                          WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                            AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
                      )
                      -- Not in current definition
                      AND NOT EXISTS (
                          SELECT 1 FROM _SchemaSmith_Tables t
                          WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
                      );

                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropTableDone = TRUE;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop tables removed from product');
                SET v_DropTableDone = FALSE;
                OPEN cur_DropTables;

                drop_tables_loop: LOOP
                    FETCH cur_DropTables INTO v_DroppedTableName, v_DropTableSql;
                    IF v_DropTableDone THEN
                        LEAVE drop_tables_loop;
                    END IF;

                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop table: ', v_DroppedTableName));
                    SET @exec_sql = v_DropTableSql;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;

                    -- Remove from ProductOwnership
                    DELETE FROM SchemaSmith_ProductOwnership
                    WHERE CONVERT(ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
                      AND CONVERT(ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                      AND ObjectType = 'TABLE'
                      AND CONVERT(ObjectName USING utf8mb4) = CONVERT(v_DroppedTableName USING utf8mb4);

                    -- Also remove related indexes from ProductOwnership
                    DELETE FROM SchemaSmith_ProductOwnership
                    WHERE CONVERT(ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
                      AND CONVERT(ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                      AND ObjectType = 'INDEX'
                      AND CONVERT(SUBSTRING_INDEX(ObjectName, '.', 1) USING utf8mb4) = CONVERT(v_DroppedTableName USING utf8mb4);
                END LOOP;

                CLOSE cur_DropTables;
            END;
        END IF;
    END IF;

END//

DELIMITER ;
