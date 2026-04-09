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

    -- Cursor for CREATE TABLE statements (non-generated columns only, ordered by OrdinalPosition)
    DECLARE cur_NewTables CURSOR FOR
        SELECT
            t.TableName,
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
        GROUP BY t.TableName, t.Engine, t.RowFormat;

    -- Cursor for non-generated columns on EXISTING tables
    DECLARE cur_NewColumns CURSOR FOR
        SELECT
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

    IF p_WhatIf = 1 THEN
        -- WhatIf mode: output the actual SQL that would be executed

        -- Step 1: Show CREATE TABLE statements
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing tables');
        SET v_Done = FALSE;
        OPEN cur_NewTables;

        whatif_tables_loop: LOOP
            FETCH cur_NewTables INTO v_StatusTableName, v_Sql;
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
            FETCH cur_NewColumns INTO v_Sql;
            IF v_Done THEN
                LEAVE whatif_new_cols_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), v_Sql);
        END LOOP;

        CLOSE cur_NewColumns;

    ELSE
        -- Step 1: Create new tables (with non-generated columns only)
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing tables');
        SET v_Done = FALSE;
        OPEN cur_NewTables;

        create_tables_loop: LOOP
            FETCH cur_NewTables INTO v_StatusTableName, v_Sql;
            IF v_Done THEN
                LEAVE create_tables_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create table ', v_StatusTableName));
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
            FETCH cur_NewColumns INTO v_Sql;
            IF v_Done THEN
                LEAVE add_columns_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Add column: ', v_Sql));
            SET @exec_sql = v_Sql;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        END LOOP;

        CLOSE cur_NewColumns;

    END IF;

END//

DELIMITER ;
