-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_MissingTableAndColumnQuench//

CREATE PROCEDURE SchemaSmith_MissingTableAndColumnQuench(
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates missing tables and adds missing columns.
    -- It reads from the _SchemaSmith_Tables and _SchemaSmith_Columns temp tables
    -- which are populated by the JSON parsing in SchemaSmith_ParseTableJson.
    --
    -- Column ordering:
    --   - Non-generated columns are ordered by OrdinalPosition
    --   - Generated columns are added after non-generated columns, ordered by
    --     DependencyLevel (to handle dependencies between generated columns)
    --     then by OrdinalPosition

    DECLARE v_Done INT DEFAULT FALSE;
    DECLARE v_Sql TEXT;
    DECLARE v_StatusTableName VARCHAR(128);
    DECLARE v_StatusVariant VARCHAR(128);
    DECLARE v_NewColVariant VARCHAR(128);

    -- Cursor for CREATE TABLE statements (non-generated columns only, ordered by OrdinalPosition)
    DECLARE cur_NewTables CURSOR FOR
        SELECT
            t.TableName,
            t.VariantName,
            CONCAT(
                'CREATE TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' (',
                GROUP_CONCAT(c.ColumnScript ORDER BY c.OrdinalPosition SEPARATOR ', '),
                COALESCE(t.AutoIncrementKeyClause, ''),
                COALESCE(
                    (SELECT CONCAT(', PRIMARY KEY (', i.IndexColumns, ')')
                     FROM _SchemaSmith_Indexes i
                     WHERE i.TableName = t.TableName AND i.IsPrimaryKey = 1),
                    ''
                ),
                ') ENGINE=', COALESCE(t.Engine, 'InnoDB'),
                CASE WHEN t.RowFormat IS NOT NULL AND t.RowFormat != ''
                     THEN CONCAT(' ROW_FORMAT=', t.RowFormat)
                     ELSE '' END
            ) AS CreateTableStatement
        FROM _SchemaSmith_Tables t
        INNER JOIN _SchemaSmith_Columns c ON c.TableName = t.TableName
        WHERE t.NewTable = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        GROUP BY t.TableName, t.VariantName, t.Engine, t.RowFormat;

    -- Cursor for non-generated columns on EXISTING tables
    DECLARE cur_NewColumns CURSOR FOR
        SELECT
            c.VariantName,
            CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                   ' ADD COLUMN ', c.ColumnScript) AS AlterTableStatement
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE t.NewTable = 0
          AND c.NewColumn = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        ORDER BY c.TableName, c.OrdinalPosition;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_Done = TRUE;

    SET SESSION group_concat_max_len = 1000000;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN MissingTableAndColumnQuench');

    -- A CustomTableRestore hook restores tables being added in case they were custom-dropped
    -- (recycled) previously; mirrors the SQL Server / PostgreSQL hook.
    SET @has_custom_restore = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES
                               WHERE CONVERT(ROUTINE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                                 AND ROUTINE_NAME = 'SchemaSmith_CustomTableRestore'
                                 AND ROUTINE_TYPE = 'PROCEDURE');

    IF p_WhatIf = 1 THEN
        -- WhatIf mode: output the actual SQL that would be executed

        IF @has_custom_restore = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Attempt custom table restore for tables being added');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('CALL SchemaSmith_CustomTableRestore(''', p_DatabaseName COLLATE utf8mb4_unicode_ci, ''', ''', SchemaSmith_StripBacktickWrapping(t.TableName), ''')')
            FROM _SchemaSmith_Tables t
            WHERE t.NewTable = 1;
        END IF;

        -- Step 1: Show CREATE TABLE statements
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing tables');
        SET v_Done = FALSE;
        OPEN cur_NewTables;

        whatif_tables_loop: LOOP
            FETCH cur_NewTables INTO v_StatusTableName, v_StatusVariant, v_Sql;
            IF v_Done THEN
                LEAVE whatif_tables_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), v_Sql);
        END LOOP;

        CLOSE cur_NewTables;

        -- Step 2: Show ALTER TABLE ADD COLUMN for new columns on existing tables
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing columns to existing tables');
        SET v_Done = FALSE;
        OPEN cur_NewColumns;

        whatif_new_cols_loop: LOOP
            FETCH cur_NewColumns INTO v_NewColVariant, v_Sql;
            IF v_Done THEN
                LEAVE whatif_new_cols_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), v_Sql);
        END LOOP;

        CLOSE cur_NewColumns;

    ELSE
        -- CustomTableRestore hook: attempt to restore tables being added in case they were
        -- custom-dropped (recycled) previously, then mark any that now exist as not-new so the
        -- create step below does not recreate them empty (preserving restored data).
        IF @has_custom_restore = 1 THEN
            BEGIN
                DECLARE v_RestoreDone INT DEFAULT FALSE;
                DECLARE v_RestoreTable VARCHAR(128);
                DECLARE cur_RestoreTables CURSOR FOR
                    SELECT SchemaSmith_StripBacktickWrapping(t.TableName)
                    FROM _SchemaSmith_Tables t
                    WHERE t.NewTable = 1;
                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_RestoreDone = TRUE;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Attempt custom table restore for tables being added');
                SET v_RestoreDone = FALSE;
                OPEN cur_RestoreTables;
                restore_tables_loop: LOOP
                    FETCH cur_RestoreTables INTO v_RestoreTable;
                    IF v_RestoreDone THEN
                        LEAVE restore_tables_loop;
                    END IF;
                    SET @exec_sql = CONCAT('CALL SchemaSmith_CustomTableRestore(''', p_DatabaseName COLLATE utf8mb4_unicode_ci, ''', ''', v_RestoreTable, ''')');
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                END LOOP;
                CLOSE cur_RestoreTables;
            END;

            UPDATE _SchemaSmith_Tables t
            SET t.NewTable = 0
            WHERE t.NewTable = 1
              AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                          WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                            AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4));

            -- NewColumn was set at parse time, before the restore brought the table back, so the
            -- restored table's columns are still flagged as new. Clear the flag for any column that
            -- now exists so the add-columns step does not try to re-add it (duplicate column error).
            UPDATE _SchemaSmith_Columns c
            SET c.NewColumn = 0
            WHERE c.NewColumn = 1
              AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS ic
                          WHERE CONVERT(ic.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                            AND CONVERT(ic.TABLE_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(c.TableName) USING utf8mb4)
                            AND CONVERT(ic.COLUMN_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(c.ColumnName) USING utf8mb4));
        END IF;

        -- Step 1: Create new tables (with non-generated columns only)
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing tables');
        SET v_Done = FALSE;
        OPEN cur_NewTables;

        create_tables_loop: LOOP
            FETCH cur_NewTables INTO v_StatusTableName, v_StatusVariant, v_Sql;
            IF v_Done THEN
                LEAVE create_tables_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create table ', v_StatusTableName,
                CASE WHEN COALESCE(v_StatusVariant, '') <> '' THEN CONCAT(' (variant: ', v_StatusVariant, ')') ELSE '' END));
            SET @exec_sql = v_Sql;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        END LOOP;

        CLOSE cur_NewTables;

        -- Step 2: Add non-generated columns to existing tables
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing columns to existing tables');
        SET v_Done = FALSE;
        OPEN cur_NewColumns;

        add_columns_loop: LOOP
            FETCH cur_NewColumns INTO v_NewColVariant, v_Sql;
            IF v_Done THEN
                LEAVE add_columns_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Add column: ', v_Sql,
                CASE WHEN COALESCE(v_NewColVariant, '') <> '' THEN CONCAT(' (variant: ', v_NewColVariant, ')') ELSE '' END));
            SET @exec_sql = v_Sql;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        END LOOP;

        CLOSE cur_NewColumns;

    END IF;

END//

DELIMITER ;
