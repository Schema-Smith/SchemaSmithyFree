-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_ForeignKeyQuench//

CREATE PROCEDURE SchemaSmith_ForeignKeyQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT,
    IN p_DropForeignKeysRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates, modifies, and drops foreign keys.
    -- It reads from the _SchemaSmith_ForeignKeys temp table populated by ParseTableJson.
    -- Separated from MissingIndexesAndConstraintsQuench so it can run AFTER data delivery,
    -- avoiding the add-drop-readd cycle for circular FK dependencies.

    DECLARE v_Done INT DEFAULT FALSE;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_Done = TRUE;

    SET SESSION group_concat_max_len = 1000000;

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
        -- Per-FK progress messages, set-based (preserves the per-FK log lines).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified foreign keys');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop modified FK: ', TableName, '.', ConstraintName)
        FROM _SchemaSmith_ModifiedFKs;

        -- Snapshot the folded drop statements once: each table's FK drops (Ord 0) then its
        -- leftover auto-created index drops (Ord 1 — the index shares the FK name and survives
        -- the FK drop). Reading INFORMATION_SCHEMA once into the temp table keeps it out of the
        -- execution loop; Ord guarantees every FK drop runs before the matching index drop.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKModStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_FKModStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Two separate INSERTs (not one UNION) because a MySQL TEMPORARY table cannot be
        -- referenced twice in one statement. AUTO_INCREMENT RowId is monotonic across the two,
        -- so the FK drops (inserted first) always get lower RowIds than the index drops.
        INSERT INTO _SchemaSmith_FKModStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', ConstraintName, '`') ORDER BY ConstraintName SEPARATOR ', '))
        FROM _SchemaSmith_ModifiedFKs
        GROUP BY TableName;
        INSERT INTO _SchemaSmith_FKModStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', m.TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP INDEX `', m.ConstraintName, '`') ORDER BY m.ConstraintName SEPARATOR ', '))
        FROM _SchemaSmith_ModifiedFKs m
        JOIN INFORMATION_SCHEMA.STATISTICS s
            ON BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY s.TABLE_NAME = BINARY m.TableName
            AND BINARY s.INDEX_NAME = BINARY m.ConstraintName
            AND s.SEQ_IN_INDEX = 1
        GROUP BY m.TableName;

        SET @v_fkmod_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKModStmts);
        WHILE @v_fkmod_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FKModStmts WHERE RowId = @v_fkmod_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_fkmod_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKModStmts WHERE RowId > @v_fkmod_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKModStmts;
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
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing foreign keys');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Create FK: ', SchemaSmith_StripBacktickWrapping(f.TableName), '.', SchemaSmith_StripBacktickWrapping(f.KeyName),
            CASE WHEN COALESCE(f.VariantName, '') <> '' THEN CONCAT(' (variant: ', f.VariantName, ')') ELSE '' END)
        FROM _SchemaSmith_ForeignKeys f
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
              AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
        );

        -- Fold each table's missing-FK creates into one multi-clause ALTER, materialize, execute.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKCreateStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_FKCreateStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_FKCreateStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', f.TableName, ' ',
                      GROUP_CONCAT(
                          CONCAT(
                              'ADD CONSTRAINT ', f.KeyName,
                              ' FOREIGN KEY (', f.Columns, ')',
                              ' REFERENCES ',
                              CASE WHEN f.RelatedTableSchema IS NOT NULL AND f.RelatedTableSchema != ''
                                   THEN CONCAT('`', f.RelatedTableSchema, '`.')
                                   ELSE CONCAT('`', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.')
                              END,
                              f.RelatedTable, ' (', f.RelatedColumns, ')',
                              ' ON DELETE ', f.DeleteAction,
                              ' ON UPDATE ', f.UpdateAction)
                          ORDER BY f.KeyName SEPARATOR ', '))
        FROM _SchemaSmith_ForeignKeys f
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(f.KeyName)
              AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
        )
        GROUP BY f.TableName;

        SET @v_fkcreate_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKCreateStmts);
        WHILE @v_fkcreate_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FKCreateStmts WHERE RowId = @v_fkcreate_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_fkcreate_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKCreateStmts WHERE RowId > @v_fkcreate_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKCreateStmts;
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
    -- Gated by DropForeignKeysRemovedFromProduct (decoupled from DropUnknownIndexes):
    -- MySQL FK cleanup no longer requires enabling index drops.
    -- =========================================================================
    IF p_DropForeignKeysRemovedFromProduct = 1 THEN
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
          )
          -- Per-table tightening: a table may set DropForeignKeysRemovedFromProduct:false to protect its own FKs.
          AND COALESCE((SELECT t.DropForeignKeysRemovedFromProduct
                          FROM _SchemaSmith_Tables t
                          WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                          LIMIT 1), 1) = 1;

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown foreign keys');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                          '` DROP FOREIGN KEY `', ConstraintName, '`')
            FROM _SchemaSmith_FKsToDrop;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown foreign keys');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Drop unknown FK: ', TableName, '.', ConstraintName)
            FROM _SchemaSmith_FKsToDrop;

            -- Same folded pattern as STEP 1: FK drops (Ord 0) then leftover auto-index drops (Ord 1).
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKDropStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_FKDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            -- Two separate INSERTs (MySQL TEMPORARY table can't be referenced twice per statement);
            -- FK drops inserted first get lower RowIds than the index drops.
            INSERT INTO _SchemaSmith_FKDropStmts (Stmt)
            SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                          GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', ConstraintName, '`') ORDER BY ConstraintName SEPARATOR ', '))
            FROM _SchemaSmith_FKsToDrop
            GROUP BY TableName;
            INSERT INTO _SchemaSmith_FKDropStmts (Stmt)
            SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', d.TableName, '` ',
                          GROUP_CONCAT(CONCAT('DROP INDEX `', d.ConstraintName, '`') ORDER BY d.ConstraintName SEPARATOR ', '))
            FROM _SchemaSmith_FKsToDrop d
            JOIN INFORMATION_SCHEMA.STATISTICS s
                ON BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY s.TABLE_NAME = BINARY d.TableName
                AND BINARY s.INDEX_NAME = BINARY d.ConstraintName
                AND s.SEQ_IN_INDEX = 1
            GROUP BY d.TableName;

            SET @v_fkdrop_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKDropStmts);
            WHILE @v_fkdrop_id IS NOT NULL DO
                SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FKDropStmts WHERE RowId = @v_fkdrop_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @v_fkdrop_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKDropStmts WHERE RowId > @v_fkdrop_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKDropStmts;

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
