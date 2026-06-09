-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_ForeignKeyQuench//

CREATE PROCEDURE SchemaSmith_ForeignKeyQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates, modifies, and drops foreign keys.
    -- It reads from the _SchemaSmith_ForeignKeys temp table populated by ParseTableJson.
    -- Separated from MissingIndexesAndConstraintsQuench so it can run AFTER data delivery,
    -- avoiding the add-drop-readd cycle for circular FK dependencies.

    DECLARE v_Done INT DEFAULT FALSE;
    DECLARE v_Sql TEXT;
    DECLARE v_TableName VARCHAR(128);
    DECLARE v_KeyName VARCHAR(128);

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_Done = TRUE;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN ForeignKeyQuench');

    -- =========================================================================
    -- STEP 1: Drop FKs that need modification (different definition)
    -- =========================================================================
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedFKs;
    CREATE TEMPORARY TABLE _SchemaSmith_ModifiedFKs (
        TableName VARCHAR(128) NOT NULL,
        ConstraintName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ConstraintName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Find FKs that exist but have different definition
    -- Uses GROUP_CONCAT to aggregate KEY_COLUMN_USAGE columns for composite FK comparison,
    -- avoiding duplicate rows that would cause primary key violations on _SchemaSmith_ModifiedFKs.
    INSERT INTO _SchemaSmith_ModifiedFKs (TableName, ConstraintName)
    SELECT
        SchemaSmith_StripBacktickWrapping(f.TableName) AS TableName,
        SchemaSmith_StripBacktickWrapping(f.KeyName) AS ConstraintName
    FROM _SchemaSmith_ForeignKeys f
    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        ON BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.TableName)
        AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
        AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
    JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
        ON BINARY rc.CONSTRAINT_SCHEMA = BINARY p_DatabaseName
        AND BINARY rc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
    WHERE (
        -- Different referenced table
        BINARY rc.REFERENCED_TABLE_NAME != BINARY SchemaSmith_StripBacktickWrapping(f.RelatedTable)
        -- Or different delete action
        OR BINARY rc.DELETE_RULE != BINARY COALESCE(f.DeleteAction, 'NO ACTION')
        -- Or different update action
        OR BINARY rc.UPDATE_RULE != BINARY COALESCE(f.UpdateAction, 'NO ACTION')
        -- Or different columns (aggregate comparison handles composite FKs;
        -- REPLACE strips backticks from comma-separated column lists like `Col1`,`Col2`)
        OR BINARY (SELECT GROUP_CONCAT(kcu.COLUMN_NAME ORDER BY kcu.ORDINAL_POSITION)
                     FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    WHERE BINARY kcu.CONSTRAINT_SCHEMA = BINARY p_DatabaseName
                      AND BINARY kcu.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
                      AND kcu.REFERENCED_TABLE_NAME IS NOT NULL)
           != BINARY REPLACE(f.Columns, '`', '')
        -- Or different referenced columns
        OR BINARY (SELECT GROUP_CONCAT(kcu.REFERENCED_COLUMN_NAME ORDER BY kcu.ORDINAL_POSITION)
                     FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    WHERE BINARY kcu.CONSTRAINT_SCHEMA = BINARY p_DatabaseName
                      AND BINARY kcu.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
                      AND kcu.REFERENCED_TABLE_NAME IS NOT NULL)
           != BINARY REPLACE(f.RelatedColumns, '`', '')
    );

    -- Drop modified FKs
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified foreign keys');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP FOREIGN KEY `', ConstraintName, '`')
        FROM _SchemaSmith_ModifiedFKs;
    ELSE
        BEGIN
            DECLARE v_FKModDone INT DEFAULT FALSE;
            DECLARE v_FKModTable VARCHAR(128);
            DECLARE v_FKModName VARCHAR(128);
            DECLARE v_FKModSql TEXT;

            DECLARE cur_ModifiedFKs CURSOR FOR
                SELECT TableName, ConstraintName
                FROM _SchemaSmith_ModifiedFKs;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FKModDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified foreign keys');
            SET v_FKModDone = FALSE;
            OPEN cur_ModifiedFKs;

            drop_modified_fk_loop: LOOP
                FETCH cur_ModifiedFKs INTO v_FKModTable, v_FKModName;
                IF v_FKModDone THEN
                    LEAVE drop_modified_fk_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop modified FK: ', v_FKModTable, '.', v_FKModName));
                -- Drop the FK constraint
                SET v_FKModSql = CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', v_FKModTable,
                                        '` DROP FOREIGN KEY `', v_FKModName, '`');
                SET @exec_sql = v_FKModSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                -- Also drop the auto-created index with the same name if it exists
                -- (MySQL auto-creates an index to support the FK if one doesn't exist)
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                    WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                      AND BINARY s.TABLE_NAME = BINARY v_FKModTable
                      AND BINARY s.INDEX_NAME = BINARY v_FKModName
                ) THEN
                    SET v_FKModSql = CONCAT('DROP INDEX `', v_FKModName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', v_FKModTable, '`');
                    SET @exec_sql = v_FKModSql;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                END IF;
            END LOOP;

            CLOSE cur_ModifiedFKs;
        END;
    END IF;

    -- =========================================================================
    -- STEP 2: Create missing foreign keys
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing foreign keys');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT(
                      'ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', f.TableName,
                      ' ADD CONSTRAINT ', f.KeyName,
                      ' FOREIGN KEY (', f.Columns, ')',
                      ' REFERENCES ',
                      CASE WHEN f.RelatedTableSchema IS NOT NULL AND f.RelatedTableSchema != ''
                           THEN CONCAT('`', f.RelatedTableSchema, '`.')
                           ELSE CONCAT('`', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.')
                      END,
                      f.RelatedTable, ' (', f.RelatedColumns, ')',
                      ' ON DELETE ', f.DeleteAction,
                      ' ON UPDATE ', f.UpdateAction)
        FROM _SchemaSmith_ForeignKeys f
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
              AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
        );
    ELSE
        BEGIN
            DECLARE v_FKDone INT DEFAULT FALSE;
            DECLARE v_FKTable VARCHAR(128);
            DECLARE v_FKName VARCHAR(128);
            DECLARE v_FKVariant VARCHAR(128);
            DECLARE v_FKSql TEXT;

            DECLARE cur_MissingForeignKeys CURSOR FOR
                SELECT
                    f.TableName,
                    f.KeyName,
                    f.VariantName,
                    CONCAT(
                        'ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', f.TableName,
                        ' ADD CONSTRAINT ', f.KeyName,
                        ' FOREIGN KEY (', f.Columns, ')',
                        ' REFERENCES ',
                        CASE WHEN f.RelatedTableSchema IS NOT NULL AND f.RelatedTableSchema != ''
                             THEN CONCAT('`', f.RelatedTableSchema, '`.')
                             ELSE CONCAT('`', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.')
                        END,
                        f.RelatedTable, ' (', f.RelatedColumns, ')',
                        ' ON DELETE ', f.DeleteAction,
                        ' ON UPDATE ', f.UpdateAction
                    ) AS CreateForeignKeyStatement
                FROM _SchemaSmith_ForeignKeys f
                WHERE NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
                      AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.TableName)
                      AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
                      AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FKDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing foreign keys');
            SET v_FKDone = FALSE;
            OPEN cur_MissingForeignKeys;

            create_fks_loop: LOOP
                FETCH cur_MissingForeignKeys INTO v_FKTable, v_FKName, v_FKVariant, v_FKSql;
                IF v_FKDone THEN
                    LEAVE create_fks_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create FK: ', v_FKTable, '.', v_FKName,
                    CASE WHEN COALESCE(v_FKVariant, '') <> '' THEN CONCAT(' (variant: ', v_FKVariant, ')') ELSE '' END));
                SET @exec_sql = v_FKSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_MissingForeignKeys;
        END;
    END IF;

    -- =========================================================================
    -- STEP 3: Update ProductOwnership for managed foreign keys
    -- =========================================================================
    IF p_WhatIf = 0 THEN
        -- Track foreign keys
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'FOREIGN KEY',
               CONCAT(SchemaSmith_StripBacktickWrapping(f.TableName), '.', SchemaSmith_StripBacktickWrapping(f.KeyName))
        FROM _SchemaSmith_ForeignKeys f
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
              AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
        );
    END IF;

    -- =========================================================================
    -- STEP 4: Drop FKs owned by product but not in definition
    -- =========================================================================
    IF p_DropUnknownIndexes = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_FKsToDrop (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Find FKs owned by product but not in current definition
        INSERT INTO _SchemaSmith_FKsToDrop (TableName, ConstraintName)
        SELECT
            SUBSTRING_INDEX(po.ObjectName, '.', 1) AS TableName,
            SUBSTRING_INDEX(po.ObjectName, '.', -1) AS ConstraintName
        FROM SchemaSmith_ProductOwnership po
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'FOREIGN KEY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_ForeignKeys f
              WHERE CONVERT(SchemaSmith_StripBacktickWrapping(f.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(SchemaSmith_StripBacktickWrapping(f.KeyName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
          )
          -- Verify FK actually exists
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
              WHERE CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
                AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
          );

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown foreign keys');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                          '` DROP FOREIGN KEY `', ConstraintName, '`')
            FROM _SchemaSmith_FKsToDrop;
        ELSE
            BEGIN
                DECLARE v_FKDropDone2 INT DEFAULT FALSE;
                DECLARE v_FKDropTable VARCHAR(128);
                DECLARE v_FKDropName VARCHAR(128);
                DECLARE v_FKDropSql2 TEXT;

                DECLARE cur_DropFKs CURSOR FOR
                    SELECT TableName, ConstraintName
                    FROM _SchemaSmith_FKsToDrop;

                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FKDropDone2 = TRUE;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown foreign keys');
                SET v_FKDropDone2 = FALSE;
                OPEN cur_DropFKs;

                drop_fks_loop: LOOP
                    FETCH cur_DropFKs INTO v_FKDropTable, v_FKDropName;
                    IF v_FKDropDone2 THEN
                        LEAVE drop_fks_loop;
                    END IF;

                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop unknown FK: ', v_FKDropTable, '.', v_FKDropName));
                    -- Drop the FK constraint
                    SET v_FKDropSql2 = CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', v_FKDropTable,
                                              '` DROP FOREIGN KEY `', v_FKDropName, '`');
                    SET @exec_sql = v_FKDropSql2;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;

                    -- Also drop the auto-created index with the same name if it exists
                    IF EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                          AND BINARY s.TABLE_NAME = BINARY v_FKDropTable
                          AND BINARY s.INDEX_NAME = BINARY v_FKDropName
                    ) THEN
                        SET v_FKDropSql2 = CONCAT('DROP INDEX `', v_FKDropName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', v_FKDropTable, '`');
                        SET @exec_sql = v_FKDropSql2;
                        PREPARE stmt FROM @exec_sql;
                        EXECUTE stmt;
                        DEALLOCATE PREPARE stmt;
                    END IF;
                END LOOP;

                CLOSE cur_DropFKs;
            END;

            -- Remove dropped FKs from ProductOwnership
            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_FKsToDrop ftd
                ON po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ftd.TableName, '.', ftd.ConstraintName) COLLATE utf8mb4_unicode_ci
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'FOREIGN KEY' COLLATE utf8mb4_unicode_ci;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsToDrop;
    END IF;

    -- Cleanup temporary tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedFKs;

END//

DELIMITER ;
