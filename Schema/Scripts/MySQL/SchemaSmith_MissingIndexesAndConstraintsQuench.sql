-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_MissingIndexesAndConstraintsQuench//

CREATE PROCEDURE SchemaSmith_MissingIndexesAndConstraintsQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT,
    IN p_DropCheckConstraintsRemovedFromProduct TINYINT,
    IN p_DropIndexesRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates, modifies, renames, and drops indexes and check constraints.
    -- It reads from the _SchemaSmith_Indexes and _SchemaSmith_CheckConstraints
    -- temp tables populated by ParseTableJson.
    -- Foreign keys are handled separately by SchemaSmith_ForeignKeyQuench.
    --
    -- Execution model: each per-row DDL loop materializes its statements (plus the exact
    -- per-object progress message) into an AUTO_INCREMENT temp table, then a WHILE loop
    -- drains it by ascending RowId (SELECT ... INTO + PREPARE/EXECUTE). This replaces the
    -- old cursors and matches the materialize-then-WHILE idiom used by ForeignKeyQuench.
    -- Detection queries (STEP 1..STEP 7) read LIVE INFORMATION_SCHEMA because each must see
    -- current catalog state relative to THIS proc's own earlier mutations (e.g. STEP 3 must
    -- see indexes STEP 2 just dropped; STEP 7 must see objects STEP 3/4/4.5 just created).
    --
    -- Crash-safety is localized to STEP 8 only: its ProductOwnership x INFORMATION_SCHEMA.STATISTICS
    -- query is the one the roadmap identifies as a segfault trigger when run frequently, so
    -- STEP 8 snapshots STATISTICS / KEY_COLUMN_USAGE / TABLE_CONSTRAINTS into temp tables
    -- RIGHT BEFORE its detection (current, post-create/drop/rename state) and reads those.

    SET SESSION group_concat_max_len = 1000000;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN MissingIndexesAndConstraintsQuench');

    -- =========================================================================
    -- STEP 0: Add missing generated columns (deferred from MissingTableAndColumnQuench
    -- to ensure dependencies like functions are deployed first)
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing generated columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Add generated column: ', c.TableName, '.', c.ColumnName,
            CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.GeneratedExpression IS NOT NULL
          AND TRIM(c.GeneratedExpression) != ''
          AND c.NewColumn = 1
        ORDER BY c.TableName, c.DependencyLevel, c.OrdinalPosition;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing generated columns');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_GenColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_GenColStmts (LogMsg, Stmt, AuditName)
        SELECT
            CONCAT('  Add generated column: ',
                   CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                          ' ADD COLUMN ', c.ColumnScript),
                   CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END),
            CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                   ' ADD COLUMN ', c.ColumnScript),
            CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.GeneratedExpression IS NOT NULL
          AND TRIM(c.GeneratedExpression) != ''
          AND c.NewColumn = 1
        ORDER BY c.TableName, c.DependencyLevel, c.OrdinalPosition;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenColStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_GenColStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'column', @ss_auditname, 'created');
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenColStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColStmts;
    END IF;

    -- =========================================================================
    -- STEP 1: Detect index renames (same columns, different name)
    -- =========================================================================
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    CREATE TEMPORARY TABLE _SchemaSmith_IndexRenames (
        TableName VARCHAR(128) NOT NULL,
        OldIndexName VARCHAR(128) NOT NULL,
        NewIndexName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, OldIndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Find indexes where the definition matches but the name is different
    -- An index is considered a rename candidate if:
    -- 1. The new index name doesn't exist
    -- 2. The old index exists and is owned by the product
    -- 3. The column list matches exactly
    INSERT INTO _SchemaSmith_IndexRenames (TableName, OldIndexName, NewIndexName)
    SELECT
        SchemaSmith_StripBacktickWrapping(i.TableName) AS TableName,
        CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci AS OldIndexName,
        SchemaSmith_StripBacktickWrapping(i.IndexName) AS NewIndexName
    FROM _SchemaSmith_Indexes i
    JOIN INFORMATION_SCHEMA.STATISTICS s
        ON BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
        AND s.SEQ_IN_INDEX = 1
    JOIN SchemaSmith_ProductOwnership po
        ON BINARY po.ProductName = BINARY p_ProductName
        AND BINARY po.ObjectSchema = BINARY p_DatabaseName
        AND po.ObjectType = 'INDEX'
        AND BINARY po.ObjectName = BINARY CONCAT(CONVERT(s.TABLE_NAME USING utf8mb4), '.', CONVERT(s.INDEX_NAME USING utf8mb4))
    WHERE i.IsPrimaryKey = 0
      -- New index name doesn't exist
      AND NOT EXISTS (
          SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s2
          WHERE BINARY s2.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY s2.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
            AND BINARY s2.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
      )
      -- Old index exists with same columns (compare normalized column list)
      AND BINARY SchemaSmith_NormalizeIndexColumns(i.IndexColumns) = BINARY (
          SELECT GROUP_CONCAT(
              CONCAT('`', sc.COLUMN_NAME, '`',
                     CASE WHEN BINARY sc.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
              ORDER BY sc.SEQ_IN_INDEX
              SEPARATOR ','
          )
          FROM INFORMATION_SCHEMA.STATISTICS sc
          WHERE BINARY sc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY sc.TABLE_NAME = BINARY s.TABLE_NAME
            AND BINARY sc.INDEX_NAME = BINARY s.INDEX_NAME
      )
      -- Same uniqueness
      AND i.IsUnique = (s.NON_UNIQUE = 0);

    -- Handle renames
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle index renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` RENAME INDEX `', OldIndexName, '` TO `', NewIndexName, '`')
        FROM _SchemaSmith_IndexRenames;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle index renames');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_RenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_RenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_RenameStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Rename index: ', TableName, '.', OldIndexName, ' -> ', NewIndexName),
            CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                   '` RENAME INDEX `', OldIndexName, '` TO `', NewIndexName, '`')
        FROM _SchemaSmith_IndexRenames;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_RenameStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_RenameStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_RenameStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_RenameStmts;

        -- Update ProductOwnership with the new names. Done set-based after the renames: the
        -- STEP 1 "new name doesn't exist" filter rules out rename chains (a->b where b is itself
        -- a live index), so no old name equals another rename's new name and one pass suffices.
        UPDATE SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_IndexRenames r
            ON BINARY po.ObjectName = BINARY CONCAT(r.TableName, '.', r.OldIndexName)
        SET po.ObjectName = CONCAT(r.TableName, '.', r.NewIndexName)
        WHERE BINARY po.ProductName = BINARY p_ProductName
          AND BINARY po.ObjectSchema = BINARY p_DatabaseName
          AND po.ObjectType = 'INDEX';
    END IF;

    -- =========================================================================
    -- STEP 2: Detect modified indexes (same name, different definition)
    -- =========================================================================
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;
    CREATE TEMPORARY TABLE _SchemaSmith_ModifiedIndexes (
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Find indexes where the name matches but the definition is different
    INSERT INTO _SchemaSmith_ModifiedIndexes (TableName, IndexName)
    SELECT
        SchemaSmith_StripBacktickWrapping(i.TableName) AS TableName,
        SchemaSmith_StripBacktickWrapping(i.IndexName) AS IndexName
    FROM _SchemaSmith_Indexes i
    JOIN INFORMATION_SCHEMA.STATISTICS s
        ON BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
        AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
        AND s.SEQ_IN_INDEX = 1
    WHERE i.IsPrimaryKey = 0
      -- Skip indexes that were just renamed
      AND NOT EXISTS (
          SELECT 1 FROM _SchemaSmith_IndexRenames r
          WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
            AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
      )
      -- Check if definition differs
      AND (
          -- Columns differ
          BINARY SchemaSmith_NormalizeIndexColumns(i.IndexColumns) != BINARY (
              SELECT GROUP_CONCAT(
                  CONCAT('`', sc.COLUMN_NAME, '`',
                         CASE WHEN BINARY sc.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
                  ORDER BY sc.SEQ_IN_INDEX
                  SEPARATOR ','
              )
              FROM INFORMATION_SCHEMA.STATISTICS sc
              WHERE BINARY sc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY sc.TABLE_NAME = BINARY s.TABLE_NAME
                AND BINARY sc.INDEX_NAME = BINARY s.INDEX_NAME
          )
          -- Or uniqueness differs
          OR i.IsUnique != (s.NON_UNIQUE = 0)
          -- Or visibility differs (FULLTEXT indexes don't support INVISIBLE, skip them)
          OR (BINARY UPPER(s.INDEX_TYPE) != BINARY 'FULLTEXT'
              AND i.IsVisible != SchemaSmith_IndexIsVisible(s.TABLE_SCHEMA, s.TABLE_NAME, s.INDEX_NAME))
      );

    -- Drop modified indexes (they'll be recreated later)
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_ModifiedIndexes;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModIdxStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_DropModIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_DropModIdxStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Drop and recreate index: ', TableName, '.', IndexName),
            CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_ModifiedIndexes;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModIdxStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_DropModIdxStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModIdxStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModIdxStmts;
    END IF;

    -- =========================================================================
    -- STEP 3: Create missing indexes (non-primary)
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT(
                      'CREATE ',
                      CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL '
                           WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE '
                           ELSE '' END,
                      'INDEX ', i.IndexName,
                      ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', i.TableName,
                      ' (', i.IndexColumns, ')',
                      CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                           WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                           ELSE '' END)
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          -- Not already renamed
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND r.NewIndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
              WHERE CONVERT(s.TABLE_SCHEMA USING utf8mb4) COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                AND CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateIdxStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CreateIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Detection reads LIVE INFORMATION_SCHEMA.STATISTICS so a just-dropped modified index
        -- (STEP 2) is correctly seen as missing here and recreated.
        INSERT INTO _SchemaSmith_CreateIdxStmts (LogMsg, Stmt, AuditName)
        SELECT
            CONCAT('  Create index: ', SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName),
                CASE WHEN COALESCE(i.VariantName, '') <> '' THEN CONCAT(' (variant: ', i.VariantName, ')') ELSE '' END),
            CONCAT(
                'CREATE ',
                CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL '
                     WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE '
                     ELSE '' END,
                'INDEX ', i.IndexName,
                ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', i.TableName,
                ' (', i.IndexColumns, ')',
                CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                     WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                     ELSE '' END,
                CASE WHEN i.IsVisible = 0 THEN ' INVISIBLE' ELSE '' END
            ),
            CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND r.NewIndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
              WHERE CONVERT(s.TABLE_SCHEMA USING utf8mb4) COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                AND CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          );

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateIdxStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_CreateIdxStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'index', @ss_auditname, 'created');
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateIdxStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateIdxStmts;
    END IF;

    -- =========================================================================
    -- STEP 3.5: Drop modified check constraints (table-level AND column-level)
    -- =========================================================================
    -- MySQL has no DDL to change a CHECK expression in place, so a constraint whose live
    -- expression no longer matches the desired one is dropped here and re-created by the
    -- create passes below (STEP 4 for table-level, STEP 4.5 for column-level). This closes
    -- the Bug 2 gap (a modified TABLE-level check used to drift silently) and gives parity
    -- with SQL Server / PostgreSQL, which both drop-then-recreate a changed check.
    --
    -- Comparison uses SchemaSmith_NormalizeCheckExpression on BOTH sides so an unchanged
    -- check is NOT phantom-dropped: MySQL reformats CHECK_CLAUSE on storage (adds an outer
    -- paren pair and normalizes spacing), which the normalizer collapses to a canonical form.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedChecks;
    CREATE TEMPORARY TABLE _SchemaSmith_ModifiedChecks (
        TableName VARCHAR(128) NOT NULL,
        ConstraintName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ConstraintName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Table-level checks: live CHECK_CLAUSE differs from the desired _SchemaSmith_CheckConstraints.Expression
    INSERT IGNORE INTO _SchemaSmith_ModifiedChecks (TableName, ConstraintName)
    SELECT
        SchemaSmith_StripBacktickWrapping(c.TableName) AS TableName,
        SchemaSmith_StripBacktickWrapping(c.ConstraintName) AS ConstraintName
    FROM _SchemaSmith_CheckConstraints c
    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        ON BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
        AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
        AND tc.CONSTRAINT_TYPE = 'CHECK'
    JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
        ON BINARY cc.CONSTRAINT_SCHEMA = BINARY p_DatabaseName
        AND BINARY cc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
    WHERE BINARY SchemaSmith_NormalizeCheckExpression(CONVERT(cc.CHECK_CLAUSE USING utf8mb4))
        != BINARY SchemaSmith_NormalizeCheckExpression(c.Expression);

    -- Column-level checks: keyed on the deterministic name CK_<table>_<column>; live CHECK_CLAUSE
    -- differs from the desired column CheckExpression. (INFORMATION_SCHEMA.CHECK_CONSTRAINTS has
    -- no column linkage, which is exactly why column checks carry a deterministic name.)
    INSERT IGNORE INTO _SchemaSmith_ModifiedChecks (TableName, ConstraintName)
    SELECT
        SchemaSmith_StripBacktickWrapping(col.TableName) AS TableName,
        CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName)) AS ConstraintName
    FROM _SchemaSmith_Columns col
    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        ON BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(col.TableName)
        AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName))
        AND tc.CONSTRAINT_TYPE = 'CHECK'
    JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
        ON BINARY cc.CONSTRAINT_SCHEMA = BINARY p_DatabaseName
        AND BINARY cc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName))
    WHERE col.CheckExpression IS NOT NULL
      AND TRIM(col.CheckExpression) != ''
      AND BINARY SchemaSmith_NormalizeCheckExpression(CONVERT(cc.CHECK_CLAUSE USING utf8mb4))
        != BINARY SchemaSmith_NormalizeCheckExpression(col.CheckExpression);

    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop modified check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`')
        FROM _SchemaSmith_ModifiedChecks;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop modified check constraints');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModChkStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_DropModChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_DropModChkStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Drop modified check constraint: ', TableName, '.', ConstraintName),
            CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`')
        FROM _SchemaSmith_ModifiedChecks;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModChkStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_DropModChkStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModChkStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModChkStmts;
    END IF;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedChecks;

    -- =========================================================================
    -- No-drop protection tier (#270): capture check constraints that WOULD have been dropped by
    -- absence but are suppressed. Same by-absence predicate as the _SchemaSmith_ChecksToDropByAbsence
    -- build below, minus the env p_DropCheckConstraintsRemovedFromProduct gate (protection forces it
    -- false, so the drop pass is skipped) but keeping the per-table opt-out. Materialize the
    -- INFORMATION_SCHEMA read into a temp first (crash-safety), then a discrete audit insert. Audit
    -- rows only, so it runs regardless of p_WhatIf. The capture signal is the session user-variable
    -- @ss_capture_would_drop set by the caller on the connection (this proc takes no new parameter).
    -- ObjectName mirrors the drop pass's 'constraint'/'dropped' audit: CONCAT(TableName, '.', ConstraintName).
    IF COALESCE(@ss_capture_would_drop, 0) = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropChecks;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropChecks (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_WouldDropChecks (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
               CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN _SchemaSmith_Tables t
            ON BINARY SchemaSmith_StripBacktickWrapping(t.TableName) = BINARY tc.TABLE_NAME
            AND t.NewTable = 0
            AND COALESCE(t.DropCheckConstraintsRemovedFromProduct, 1) = 1
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'CHECK'
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_CheckConstraints c
              WHERE BINARY SchemaSmith_StripBacktickWrapping(c.TableName) = BINARY tc.TABLE_NAME
                AND BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName) = BINARY tc.CONSTRAINT_NAME)
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Columns col
              WHERE BINARY SchemaSmith_StripBacktickWrapping(col.TableName) = BINARY tc.TABLE_NAME
                AND col.CheckExpression IS NOT NULL AND TRIM(col.CheckExpression) != ''
                AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName)));

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'constraint', CONCAT(TableName, '.', ConstraintName), 'wouldDrop'
        FROM _SchemaSmith_WouldDropChecks;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropChecks;
    END IF;

    -- =========================================================================
    -- Drop check constraints removed from the product (by-absence), gated by the cascade flag
    -- and per-table tightening. Scoped to the current quench's product tables. Table-level checks
    -- absent from _SchemaSmith_CheckConstraints are dropped; a column-level CK_<table>_<column>
    -- check is excluded only while its column still carries a CheckExpression (then it is owned by
    -- the modify/create passes); once the CheckExpression is removed, the orphan is cleaned up here.
    -- =========================================================================
    IF p_DropCheckConstraintsRemovedFromProduct = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ChecksToDropByAbsence;
        CREATE TEMPORARY TABLE _SchemaSmith_ChecksToDropByAbsence (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_ChecksToDropByAbsence (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
               CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN _SchemaSmith_Tables t
            ON BINARY SchemaSmith_StripBacktickWrapping(t.TableName) = BINARY tc.TABLE_NAME
            AND t.NewTable = 0
            AND COALESCE(t.DropCheckConstraintsRemovedFromProduct, 1) = 1
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'CHECK'
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_CheckConstraints c
              WHERE BINARY SchemaSmith_StripBacktickWrapping(c.TableName) = BINARY tc.TABLE_NAME
                AND BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName) = BINARY tc.CONSTRAINT_NAME)
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Columns col
              WHERE BINARY SchemaSmith_StripBacktickWrapping(col.TableName) = BINARY tc.TABLE_NAME
                AND col.CheckExpression IS NOT NULL AND TRIM(col.CheckExpression) != ''
                AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName)));

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop check constraints removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`')
            FROM _SchemaSmith_ChecksToDropByAbsence;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop check constraints removed from product');

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropAbsChkStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_DropAbsChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT, AuditName TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_DropAbsChkStmts (Stmt, AuditName)
            SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`'),
                   CONCAT(TableName, '.', ConstraintName)
            FROM _SchemaSmith_ChecksToDropByAbsence;

            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropAbsChkStmts);
            WHILE @ss_id IS NOT NULL DO
                SELECT Stmt, AuditName INTO @exec_sql, @ss_auditname FROM _SchemaSmith_DropAbsChkStmts WHERE RowId = @ss_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
                INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'constraint', @ss_auditname, 'dropped');
                DEALLOCATE PREPARE stmt;
                SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropAbsChkStmts WHERE RowId > @ss_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropAbsChkStmts;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ChecksToDropByAbsence;
    END IF;

    -- =========================================================================
    -- STEP 4: Create missing check constraints (MySQL 8.0.16+)
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' ADD CONSTRAINT ', c.ConstraintName,
                      ' CHECK (', c.Expression, ')')
        FROM _SchemaSmith_CheckConstraints c
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing check constraints');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateChkStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CreateChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Detection reads LIVE INFORMATION_SCHEMA.TABLE_CONSTRAINTS so a just-dropped modified
        -- table check (STEP 3.5) is correctly seen as missing here and recreated.
        INSERT INTO _SchemaSmith_CreateChkStmts (LogMsg, Stmt, AuditName)
        SELECT
            CONCAT('  Create check constraint: ', c.TableName, '.', c.ConstraintName,
                CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END),
            CONCAT(
                'ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                ' ADD CONSTRAINT ', c.ConstraintName,
                ' CHECK (', c.Expression, ')'
            ),
            CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
        FROM _SchemaSmith_CheckConstraints c
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateChkStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_CreateChkStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'constraint', @ss_auditname, 'created');
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateChkStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateChkStmts;
    END IF;

    -- =========================================================================
    -- STEP 4.5: Create missing column-level check constraints (MySQL 8.0.16+)
    -- =========================================================================
    -- A column's CheckExpression becomes a deterministically named CK_<table>_<column> check.
    -- The deterministic name lets the create/modify passes key on it (INFORMATION_SCHEMA has no
    -- column linkage for checks). Mirrors the table-level STEP 4 idiom exactly.
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing column check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' ADD CONSTRAINT `CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`',
                      ' CHECK (', c.CheckExpression, ')')
        FROM _SchemaSmith_Columns c
        WHERE c.CheckExpression IS NOT NULL
          AND TRIM(c.CheckExpression) != ''
          AND NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing column check constraints');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateColChkStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CreateColChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Detection reads LIVE INFORMATION_SCHEMA.TABLE_CONSTRAINTS so a just-dropped modified
        -- column check (STEP 3.5) is correctly seen as missing here and recreated.
        INSERT INTO _SchemaSmith_CreateColChkStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Create column check constraint: ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.',
                   CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName)),
                CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END),
            CONCAT(
                'ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                ' ADD CONSTRAINT `CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`',
                ' CHECK (', c.CheckExpression, ')'
            )
        FROM _SchemaSmith_Columns c
        WHERE c.CheckExpression IS NOT NULL
          AND TRIM(c.CheckExpression) != ''
          AND NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateColChkStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_CreateColChkStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateColChkStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateColChkStmts;
    END IF;

    -- =========================================================================
    -- STEP 7: Update ProductOwnership for managed objects
    -- Reads LIVE INFORMATION_SCHEMA so it confirms objects created THIS run (STEP 3/4/4.5).
    -- =========================================================================
    IF p_WhatIf = 0 THEN
        -- Track indexes
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'INDEX',
               CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
        FROM _SchemaSmith_Indexes i
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
              AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
        );

        -- Track check constraints
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'CHECK CONSTRAINT',
               CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
        FROM _SchemaSmith_CheckConstraints c
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );

        -- Track column-level check constraints (deterministic CK_<table>_<column> name)
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'CHECK CONSTRAINT',
               CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.CK_',
                      SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        FROM _SchemaSmith_Columns c
        WHERE c.CheckExpression IS NOT NULL
          AND TRIM(c.CheckExpression) != ''
          AND EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );
    END IF;

    -- =========================================================================
    -- No-drop protection tier (#270): capture indexes that WOULD have been dropped by absence but
    -- are suppressed. STEP 8 below is skipped entirely under protection (the caller forces both
    -- p_DropUnknownIndexes and p_DropIndexesRemovedFromProduct false), so its _SchemaSmith_Step8Idx
    -- snapshot is never built; this block is self-contained — it snapshots the catalog
    -- (INFORMATION_SCHEMA out of the set-based DML, #337 crash-safety), then computes BOTH STEP 8
    -- axes' by-absence candidates MINUS their env gates into one temp — AXIS 1 removed-from-product
    -- (keeps the per-table COALESCE(t.DropIndexesRemovedFromProduct,1) opt-out, PRIMARY/rename/
    -- owned-by-product exclusions) UNION AXIS 2 unknown/out-of-band — then a discrete audit insert.
    -- Modified/for-change indexes are NOT captured: they remain in _SchemaSmith_Indexes so both axes'
    -- "not in current definition" predicate excludes them (STEP 2 drops-then-recreates them, a
    -- transient change, never a protection-withheld drop). Audit rows only, so it runs regardless of
    -- p_WhatIf. The capture signal is the session user-variable @ss_capture_would_drop set by the
    -- caller on the connection (this proc takes no new parameter). ObjectName/ObjectType mirror STEP
    -- 8's 'index'/'dropped' audit: ObjectType 'index', ObjectName CONCAT(TableName, '.', IndexName).
    IF COALESCE(@ss_capture_would_drop, 0) = 1 THEN
        -- Catalog snapshot (one row per index; SEQ_IN_INDEX = 1). Mirrors STEP 8's _SchemaSmith_Step8Idx.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8IdxCat;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropStep8IdxCat (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_WouldDropStep8IdxCat (TableName, IndexName)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        -- Defined-index snapshot (table.index pairs from the current definition). This is the
        -- "not in current definition" bridge and is exactly what keeps modified indexes (still in
        -- _SchemaSmith_Indexes) out of the capture set.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8DefIdx;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropStep8DefIdx (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_WouldDropStep8DefIdx (TableName, IndexName)
        SELECT CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4),
               CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4)
        FROM _SchemaSmith_Indexes i;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8Indexes;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropStep8Indexes (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- AXIS 1 — removed-from-product (env gate p_DropIndexesRemovedFromProduct dropped; per-table opt-out kept).
        INSERT IGNORE INTO _SchemaSmith_WouldDropStep8Indexes (TableName, IndexName)
        SELECT DISTINCT
            SUBSTRING_INDEX(po.ObjectName, '.', 1),
            SUBSTRING_INDEX(po.ObjectName, '.', -1)
        FROM SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
        LEFT JOIN _SchemaSmith_WouldDropStep8DefIdx di
            ON CONVERT(di.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        LEFT JOIN _SchemaSmith_WouldDropStep8IdxCat ei
            ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
          -- Per-table tightening (the env/product gate is removed for capture)
          AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
          -- Never drop PRIMARY KEY
          AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition (LEFT JOIN produces NULL when not found) — also excludes modified indexes
          AND di.IndexName IS NULL
          -- Not a renamed index
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName = SUBSTRING_INDEX(po.ObjectName, '.', 1)
                AND r.OldIndexName = SUBSTRING_INDEX(po.ObjectName, '.', -1)
          )
          -- Verify index actually exists (snapshot bridge)
          AND ei.IndexName IS NOT NULL;

        -- AXIS 2 — out-of-band (env gate p_DropUnknownIndexes dropped).
        INSERT IGNORE INTO _SchemaSmith_WouldDropStep8Indexes (TableName, IndexName)
        SELECT CONVERT(ei.TableName USING utf8mb4), CONVERT(ei.IndexName USING utf8mb4)
        FROM _SchemaSmith_WouldDropStep8IdxCat ei
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
        WHERE UPPER(ei.IndexName COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition — also excludes modified indexes (still present in _SchemaSmith_Indexes)
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_WouldDropStep8DefIdx di
              WHERE CONVERT(di.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
          )
          -- Not owned by this product
          AND NOT EXISTS (
              SELECT 1 FROM SchemaSmith_ProductOwnership po
              WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
                AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
                AND po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ei.TableName, '.', ei.IndexName) COLLATE utf8mb4_unicode_ci
          )
          -- Not a renamed index
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE CONVERT(r.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                AND CONVERT(r.OldIndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
          );

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'index', CONCAT(TableName, '.', IndexName), 'wouldDrop'
        FROM _SchemaSmith_WouldDropStep8Indexes;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8Indexes;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8DefIdx;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8IdxCat;
    END IF;

    -- =========================================================================
    -- STEP 8: Drop indexes not in the definition — two disjoint axes, matching SQL Server / PostgreSQL:
    --   * removed-from-product (owned by this product, no longer in the JSON) — gated by
    --     p_DropIndexesRemovedFromProduct (default on) + per-table tightening.
    --   * out-of-band (in the catalog, NOT owned by this product) — gated by p_DropUnknownIndexes
    --     (default off).
    -- =========================================================================
    IF p_DropUnknownIndexes = 1 OR p_DropIndexesRemovedFromProduct = 1 THEN
        -- Crash-safe snapshot for STEP 8 ONLY. Taken HERE (after STEP 0..7's creates/drops/renames)
        -- so it reflects the current catalog state, exactly what the original live reads saw at this
        -- point. STEP 8's ProductOwnership x STATISTICS detection is the frequent-run segfault trigger
        -- the roadmap identifies, so it (and its FK-drop join) read these snapshots instead of live
        -- INFORMATION_SCHEMA. SEQ_IN_INDEX = 1 yields one row per index (NON_UNIQUE is index-level),
        -- collapsing the original LEFT JOIN s + EXISTS s2 into a single snapshot reference (no 1137).
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8Idx;
        CREATE TEMPORARY TABLE _SchemaSmith_Step8Idx (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            NonUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_Step8Idx (TableName, IndexName, NonUnique)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4), s.NON_UNIQUE
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        -- FK rows referencing a product table (for the FK-before-index drop join). Same-schema FKs.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8KCU;
        CREATE TEMPORARY TABLE _SchemaSmith_Step8KCU (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            ReferencedTableName VARCHAR(128) DEFAULT NULL,
            KEY ix_s8kcu (ReferencedTableName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_Step8KCU (TableName, ConstraintName, ReferencedTableName)
        SELECT CONVERT(kcu.TABLE_NAME USING utf8mb4), CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4), CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
        WHERE BINARY kcu.REFERENCED_TABLE_SCHEMA = BINARY p_DatabaseName
          AND BINARY kcu.TABLE_SCHEMA = BINARY p_DatabaseName
          AND kcu.REFERENCED_TABLE_NAME IS NOT NULL;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8TC;
        CREATE TEMPORARY TABLE _SchemaSmith_Step8TC (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            KEY ix_s8tc (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_Step8TC (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4), CONVERT(tc.CONSTRAINT_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY';

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexesToDrop (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            IsUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- AXIS 1 — removed-from-product: indexes OWNED by this product but no longer in the
        -- definition. Gated by p_DropIndexesRemovedFromProduct (env/product) + per-table tightening.
        -- IMPORTANT: Only consider indexes on tables that ARE in the current definition (_SchemaSmith_Tables)
        -- so an index on a table not in the current JSON is never dropped.
        -- Reads _SchemaSmith_Step8Idx (single LEFT JOIN): existence == ei.IndexName IS NOT NULL,
        -- IsUnique == (ei.NonUnique = 0) -- collapses the original STATISTICS s + s2 references.
        IF p_DropIndexesRemovedFromProduct = 1 THEN
            INSERT INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
            SELECT
                SUBSTRING_INDEX(po.ObjectName, '.', 1) AS TableName,
                SUBSTRING_INDEX(po.ObjectName, '.', -1) AS IndexName,
                COALESCE(ei.NonUnique = 0, 0) AS IsUnique
            FROM SchemaSmith_ProductOwnership po
            -- Join with _SchemaSmith_Tables to only consider indexes on tables in the current definition
            INNER JOIN _SchemaSmith_Tables t
                ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            LEFT JOIN _SchemaSmith_Step8Idx ei
                ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            LEFT JOIN _SchemaSmith_Indexes i
                ON CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
              -- Per-table tightening (the env/product gate is the enclosing IF)
              AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
              -- Never drop PRIMARY KEY
              AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
              -- Not in current definition (LEFT JOIN produces NULL when not found)
              AND i.IndexName IS NULL
              -- Not a renamed index
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE r.TableName = SUBSTRING_INDEX(po.ObjectName, '.', 1)
                    AND r.OldIndexName = SUBSTRING_INDEX(po.ObjectName, '.', -1)
              )
              -- Verify index actually exists
              AND ei.IndexName IS NOT NULL;
        END IF;

        -- AXIS 2 — out-of-band: an index present in the catalog on a current-quench table that is
        -- NOT in the definition AND NOT owned by this product (e.g. hand-created via DDL). Gated by
        -- p_DropUnknownIndexes. Crash-safe: reads the Step8Idx snapshot + temp/real tables only —
        -- never INFORMATION_SCHEMA in this set-based DML (#337). INSERT IGNORE dedupes against any
        -- removed-from-product row already queued (shared PK on _SchemaSmith_IndexesToDrop).
        IF p_DropUnknownIndexes = 1 THEN
            INSERT IGNORE INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
            SELECT CONVERT(ei.TableName USING utf8mb4), CONVERT(ei.IndexName USING utf8mb4), COALESCE(ei.NonUnique = 0, 0)
            FROM _SchemaSmith_Step8Idx ei
            INNER JOIN _SchemaSmith_Tables t
                ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
            WHERE UPPER(ei.IndexName COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Indexes i
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                    AND CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM SchemaSmith_ProductOwnership po
                  WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
                    AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                    AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
                    AND po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ei.TableName, '.', ei.IndexName) COLLATE utf8mb4_unicode_ci
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE CONVERT(r.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                    AND CONVERT(r.OldIndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
              );
        END IF;

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
            FROM _SchemaSmith_IndexesToDrop;
        ELSE
            -- First, drop any foreign keys that reference unique indexes we're about to drop.
            -- FK drops MUST complete before the index drops below (a FK backed by a unique index
            -- blocks dropping that index); these are two sequential loops, preserving that order.
            -- Reads the STEP 8 snapshots (Step8Idx existence bridge, Step8KCU, Step8TC).
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropFKForIdxStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_DropFKForIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            -- DISTINCT: one DROP FOREIGN KEY per FK. A composite FK yields multiple KEY_COLUMN_USAGE
            -- rows (one per column), and several dropped indexes on the same referenced table re-join
            -- the same FK; both would otherwise emit a duplicate DROP FOREIGN KEY (error 1091 on the
            -- second). The original per-row cursor had the same latent duplication.
            INSERT INTO _SchemaSmith_DropFKForIdxStmts (LogMsg, Stmt)
            SELECT DISTINCT
                CONCAT('  Drop FK for index: ',
                       CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', kcu.TableName, '` DROP FOREIGN KEY `', tc.ConstraintName, '`')),
                CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', kcu.TableName, '` DROP FOREIGN KEY `', tc.ConstraintName, '`')
            FROM _SchemaSmith_IndexesToDrop itd
            JOIN _SchemaSmith_Step8Idx ei
                ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
                AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(itd.IndexName USING utf8mb4)
            JOIN _SchemaSmith_Step8KCU kcu
                ON CONVERT(kcu.ReferencedTableName USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
            JOIN _SchemaSmith_Step8TC tc
                ON CONVERT(tc.TableName USING utf8mb4) = CONVERT(kcu.TableName USING utf8mb4)
                AND CONVERT(tc.ConstraintName USING utf8mb4) = CONVERT(kcu.ConstraintName USING utf8mb4)
            WHERE itd.IsUnique = 1;

            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropFKForIdxStmts);
            WHILE @ss_id IS NOT NULL DO
                SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_DropFKForIdxStmts WHERE RowId = @ss_id;
                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropFKForIdxStmts WHERE RowId > @ss_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropFKForIdxStmts;

            -- Now drop the unknown indexes
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropIdxStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_DropIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_DropIdxStmts (LogMsg, Stmt, AuditName)
            SELECT
                CONCAT('  Drop unknown index: ', TableName, '.', IndexName),
                CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`'),
                CONCAT(TableName, '.', IndexName)
            FROM _SchemaSmith_IndexesToDrop;

            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropIdxStmts);
            WHILE @ss_id IS NOT NULL DO
                SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_DropIdxStmts WHERE RowId = @ss_id;
                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
                INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'index', @ss_auditname, 'dropped');
                DEALLOCATE PREPARE stmt;
                SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropIdxStmts WHERE RowId > @ss_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropIdxStmts;

            -- Remove dropped indexes from ProductOwnership
            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_IndexesToDrop itd
                ON po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(itd.TableName, '.', itd.IndexName) COLLATE utf8mb4_unicode_ci
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8Idx;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8KCU;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8TC;
    END IF;

    -- Cleanup temporary tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;

END//

DELIMITER ;
