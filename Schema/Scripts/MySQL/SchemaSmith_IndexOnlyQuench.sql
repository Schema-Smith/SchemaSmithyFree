-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_IndexOnlyQuench//

CREATE PROCEDURE SchemaSmith_IndexOnlyQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT,
    IN p_DropIndexesRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure handles index-only quenching.
    -- It creates, modifies, and drops indexes but does NOT touch:
    -- - Table structure (columns, data types)
    -- - Foreign keys
    -- - Check constraints
    --
    -- It reads from the _SchemaSmith_Indexes and _SchemaSmith_FullTextIndexes
    -- temp tables populated by ParseTableJson.


    SET SESSION group_concat_max_len = 1000000;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN IndexOnlyQuench');

    -- Ensure _SchemaSmith_FullTextIndexes exists (ParseTableJson may not create it if there are no fulltext indexes).
    -- Keep this fallback schema in lockstep with ParseTableJson's primary definition (see
    -- SchemaSmith_ParseTableJson.sql) — both use a synthetic RowId so two same-named entries
    -- with mutually exclusive ShouldApplyExpression can coexist until the ShouldApply DELETE pass.
    CREATE TEMPORARY TABLE IF NOT EXISTS _SchemaSmith_FullTextIndexes (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        Columns TEXT NOT NULL,
        Parser VARCHAR(128) DEFAULT NULL,
        Comment VARCHAR(255) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_ft_table_name (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Rename index: ', TableName, '.', OldIndexName, ' -> ', NewIndexName)
        FROM _SchemaSmith_IndexRenames;

        -- Fold each table's index renames into one multi-clause ALTER, materialize, execute.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexRenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IndexRenameStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('RENAME INDEX `', OldIndexName, '` TO `', NewIndexName, '`') ORDER BY OldIndexName SEPARATOR ', '))
        FROM _SchemaSmith_IndexRenames
        GROUP BY TableName;

        SET @v_rename_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexRenameStmts);
        WHILE @v_rename_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_IndexRenameStmts WHERE RowId = @v_rename_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_rename_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexRenameStmts WHERE RowId > @v_rename_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenameStmts;

        -- Update ProductOwnership with new names (set-based; mirrors the per-row UPDATE this loop replaced).
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
      -- Check if definition differs (columns, uniqueness, or index type)
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
          -- Or index type differs (BTREE vs HASH)
          OR (BINARY UPPER(COALESCE(i.IndexType, 'BTREE')) != BINARY UPPER(s.INDEX_TYPE)
              AND NOT (BINARY UPPER(COALESCE(i.IndexType, 'BTREE')) = BINARY 'BTREE' AND BINARY UPPER(s.INDEX_TYPE) = BINARY 'BTREE'))
          -- Or visibility differs (FULLTEXT indexes don't support INVISIBLE, skip them)
          OR (BINARY UPPER(s.INDEX_TYPE) != BINARY 'FULLTEXT'
              AND i.IsVisible != (CASE WHEN s.IS_VISIBLE = 'YES' THEN 1 ELSE 0 END))
      );

    -- Drop modified indexes (they'll be recreated later)
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_ModifiedIndexes;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop and recreate index: ', TableName, '.', IndexName)
        FROM _SchemaSmith_ModifiedIndexes;

        -- Fold each table's modified-index drops into one multi-clause ALTER, materialize, execute.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexModStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexModStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IndexModStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP INDEX `', IndexName, '`') ORDER BY IndexName SEPARATOR ', '))
        FROM _SchemaSmith_ModifiedIndexes
        GROUP BY TableName;

        SET @v_indexmod_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexModStmts);
        WHILE @v_indexmod_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_IndexModStmts WHERE RowId = @v_indexmod_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_indexmod_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexModStmts WHERE RowId > @v_indexmod_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexModStmts;
    END IF;

    -- =========================================================================
    -- No-drop protection tier (#270): capture indexes that WOULD have been dropped by absence but
    -- are suppressed. STEP 3 below is skipped entirely under protection (the caller forces both
    -- p_DropUnknownIndexes and p_DropIndexesRemovedFromProduct false), so this block is
    -- self-contained: it snapshots the catalog (INFORMATION_SCHEMA out of the set-based DML, #337
    -- crash-safety), then computes BOTH STEP 3 axes' by-absence candidates MINUS their env gates
    -- into one temp — AXIS 1 removed-from-product (keeps the per-table
    -- COALESCE(t.DropIndexesRemovedFromProduct,1) opt-out) UNION AXIS 2 unknown/out-of-band — then a
    -- discrete audit insert. Audit rows only, so it runs regardless of p_WhatIf. The capture signal
    -- is the session user-variable @ss_capture_would_drop set by the caller on the connection (these
    -- procs take no new parameter). ObjectName mirrors the sibling 'index'/'dropped' audit in
    -- MissingIndexesAndConstraintsQuench STEP 8: CONCAT(TableName, '.', IndexName).
    IF COALESCE(@ss_capture_would_drop, 0) = 1 THEN
        -- Catalog snapshot (one row per index; SEQ_IN_INDEX = 1). Mirrors STEP 3's _SchemaSmith_IdxOnlyIdx.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropIdxCat;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropIdxCat (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_WouldDropIdxCat (TableName, IndexName)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        -- Defined-index snapshot (table.index pairs from the current definition). Mirrors STEP 3's _SchemaSmith_DefinedIndexes.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropDefIdx;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropDefIdx (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_WouldDropDefIdx (TableName, IndexName)
        SELECT CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4),
               CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4)
        FROM _SchemaSmith_Indexes i;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropIndexes;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropIndexes (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- AXIS 1 — removed-from-product (env gate p_DropIndexesRemovedFromProduct dropped; per-table opt-out kept).
        INSERT IGNORE INTO _SchemaSmith_WouldDropIndexes (TableName, IndexName)
        SELECT DISTINCT
            SUBSTRING_INDEX(po.ObjectName, '.', 1),
            SUBSTRING_INDEX(po.ObjectName, '.', -1)
        FROM SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
        LEFT JOIN _SchemaSmith_WouldDropDefIdx di
            ON CONVERT(di.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        LEFT JOIN _SchemaSmith_WouldDropIdxCat ei
            ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci
          -- Per-table tightening (the env/product gate is removed for capture)
          AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
          -- Never drop PRIMARY KEY
          AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != 'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition (LEFT JOIN produces NULL when not found)
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
        INSERT IGNORE INTO _SchemaSmith_WouldDropIndexes (TableName, IndexName)
        SELECT CONVERT(ei.TableName USING utf8mb4), CONVERT(ei.IndexName USING utf8mb4)
        FROM _SchemaSmith_WouldDropIdxCat ei
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
        WHERE UPPER(ei.IndexName COLLATE utf8mb4_unicode_ci) != 'PRIMARY' COLLATE utf8mb4_unicode_ci
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_WouldDropDefIdx di
              WHERE CONVERT(di.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
          )
          AND NOT EXISTS (
              SELECT 1 FROM SchemaSmith_ProductOwnership po
              WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
                AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci
                AND po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ei.TableName, '.', ei.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE CONVERT(r.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                AND CONVERT(r.OldIndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
          );

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'index', CONCAT(TableName, '.', IndexName), 'wouldDrop'
        FROM _SchemaSmith_WouldDropIndexes;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropIndexes;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropDefIdx;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropIdxCat;
    END IF;

    -- =========================================================================
    -- STEP 3: Drop unknown indexes (owned by product but not in definition)
    -- =========================================================================
    IF p_DropUnknownIndexes = 1 OR p_DropIndexesRemovedFromProduct = 1 THEN
        -- Crash-safe snapshots (mirrors MissingIndexesAndConstraintsQuench STEP 8): decoupling
        -- removed-from-product from DropUnknownIndexes means this block runs on every IndexOnly
        -- quench (DropIndexesRemovedFromProduct defaults on), so detection + FK-drop read snapshots
        -- rather than live INFORMATION_SCHEMA in set-based DML (the frequent-run segfault trigger, #337).
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxOnlyIdx;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxOnlyIdx (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            NonUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IdxOnlyIdx (TableName, IndexName, NonUnique)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4), s.NON_UNIQUE
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxOnlyKCU;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxOnlyKCU (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            ReferencedTableName VARCHAR(128) DEFAULT NULL,
            KEY ix_iokcu (ReferencedTableName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IdxOnlyKCU (TableName, ConstraintName, ReferencedTableName)
        SELECT CONVERT(kcu.TABLE_NAME USING utf8mb4), CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4), CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
        WHERE BINARY kcu.REFERENCED_TABLE_SCHEMA = BINARY p_DatabaseName
          AND BINARY kcu.TABLE_SCHEMA = BINARY p_DatabaseName
          AND kcu.REFERENCED_TABLE_NAME IS NOT NULL;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxOnlyTC;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxOnlyTC (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            KEY ix_iotc (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IdxOnlyTC (TableName, ConstraintName)
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

        -- Create helper temp table to copy index definitions
        -- MySQL cannot reference the same temporary table multiple times in a query
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedIndexes;
        CREATE TEMPORARY TABLE _SchemaSmith_DefinedIndexes (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Populate with table.index pairs from the definition
        INSERT INTO _SchemaSmith_DefinedIndexes (TableName, IndexName)
        SELECT CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4),
               CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4)
        FROM _SchemaSmith_Indexes i;

        -- AXIS 1 — removed-from-product: indexes OWNED by this product but no longer in the
        -- definition. Gated by p_DropIndexesRemovedFromProduct (+ per-table tightening), NOT by
        -- p_DropUnknownIndexes (Index-B decouple, matching SQL Server / PostgreSQL).
        -- IMPORTANT: Only consider indexes on tables that ARE in the current definition (_SchemaSmith_Tables).
        -- Reads the _SchemaSmith_IdxOnlyIdx snapshot (existence == ei.IndexName IS NOT NULL,
        -- IsUnique == ei.NonUnique = 0) instead of live INFORMATION_SCHEMA.
        IF p_DropIndexesRemovedFromProduct = 1 THEN
            INSERT INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
            SELECT DISTINCT
                SUBSTRING_INDEX(po.ObjectName, '.', 1) AS TableName,
                SUBSTRING_INDEX(po.ObjectName, '.', -1) AS IndexName,
                COALESCE(ei.NonUnique = 0, 0) AS IsUnique
            FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_Tables t
                ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            LEFT JOIN _SchemaSmith_DefinedIndexes di
                ON CONVERT(di.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            LEFT JOIN _SchemaSmith_IdxOnlyIdx ei
                ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci
              -- Per-table tightening (the env/product gate is the enclosing IF)
              AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
              -- Never drop PRIMARY KEY
              AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != 'PRIMARY' COLLATE utf8mb4_unicode_ci
              -- Not in current definition (LEFT JOIN produces NULL when not found)
              AND di.IndexName IS NULL
              -- Not a renamed index
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE r.TableName = SUBSTRING_INDEX(po.ObjectName, '.', 1)
                    AND r.OldIndexName = SUBSTRING_INDEX(po.ObjectName, '.', -1)
              )
              -- Verify index actually exists (snapshot bridge)
              AND ei.IndexName IS NOT NULL;
        END IF;

        -- AXIS 2 — out-of-band: an index in the catalog on a current-quench table that is NOT in the
        -- definition AND NOT owned by this product. Gated by p_DropUnknownIndexes. Crash-safe: reads
        -- the snapshot + temp/real tables only. INSERT IGNORE dedupes against any removed-from-product
        -- row already queued (shared PK on _SchemaSmith_IndexesToDrop).
        IF p_DropUnknownIndexes = 1 THEN
            INSERT IGNORE INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
            SELECT CONVERT(ei.TableName USING utf8mb4), CONVERT(ei.IndexName USING utf8mb4), COALESCE(ei.NonUnique = 0, 0)
            FROM _SchemaSmith_IdxOnlyIdx ei
            INNER JOIN _SchemaSmith_Tables t
                ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
            WHERE UPPER(ei.IndexName COLLATE utf8mb4_unicode_ci) != 'PRIMARY' COLLATE utf8mb4_unicode_ci
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_DefinedIndexes di
                  WHERE CONVERT(di.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                    AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM SchemaSmith_ProductOwnership po
                  WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
                    AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                    AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci
                    AND po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ei.TableName, '.', ei.IndexName) COLLATE utf8mb4_unicode_ci
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE CONVERT(r.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                    AND CONVERT(r.OldIndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
              );
        END IF;

        -- Cleanup helper temp table
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedIndexes;

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
            FROM _SchemaSmith_IndexesToDrop;
        ELSE
            -- First, drop any foreign keys that reference unique indexes we're about to drop.
            -- Snapshot the referencing FKs into a temp table (grouped by table) so the drop-FK
            -- and drop-index phases each fold into one ALTER per table instead of per-row PREPAREs.
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsForIndexDrop;
            CREATE TEMPORARY TABLE _SchemaSmith_FKsForIndexDrop (
                TableName VARCHAR(128) NOT NULL,
                ConstraintName VARCHAR(128) NOT NULL,
                PRIMARY KEY (TableName, ConstraintName)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            -- DISTINCT: a composite FK produces one KEY_COLUMN_USAGE row per column, which would
            -- otherwise duplicate the (table, constraint) pair and fold into an invalid
            -- "DROP FOREIGN KEY x, DROP FOREIGN KEY x" clause.
            -- Reads the STEP 3 snapshots (IdxOnlyKCU / IdxOnlyTC), not live INFORMATION_SCHEMA.
            -- itd.IsUnique already carries the unique-index flag, so no STATISTICS re-check is needed.
            INSERT INTO _SchemaSmith_FKsForIndexDrop (TableName, ConstraintName)
            SELECT DISTINCT
                CONVERT(kcu.TableName USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                CONVERT(tc.ConstraintName USING utf8mb4) COLLATE utf8mb4_unicode_ci
            FROM _SchemaSmith_IndexesToDrop itd
            JOIN _SchemaSmith_IdxOnlyKCU kcu
                ON CONVERT(kcu.ReferencedTableName USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
            JOIN _SchemaSmith_IdxOnlyTC tc
                ON CONVERT(tc.TableName USING utf8mb4) = CONVERT(kcu.TableName USING utf8mb4)
                AND CONVERT(tc.ConstraintName USING utf8mb4) = CONVERT(kcu.ConstraintName USING utf8mb4)
            WHERE itd.IsUnique = 1;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Drop FK for index: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                          '` DROP FOREIGN KEY `', ConstraintName, '`')
            FROM _SchemaSmith_FKsForIndexDrop;

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKForIndexDropStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_FKForIndexDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_FKForIndexDropStmts (Stmt)
            SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                          GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', ConstraintName, '`') ORDER BY ConstraintName SEPARATOR ', '))
            FROM _SchemaSmith_FKsForIndexDrop
            GROUP BY TableName;

            SET @v_fkforindex_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKForIndexDropStmts);
            WHILE @v_fkforindex_id IS NOT NULL DO
                SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FKForIndexDropStmts WHERE RowId = @v_fkforindex_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @v_fkforindex_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKForIndexDropStmts WHERE RowId > @v_fkforindex_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKForIndexDropStmts;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsForIndexDrop;

            -- Now drop the unknown indexes
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Drop unknown index: ', TableName, '.', IndexName)
            FROM _SchemaSmith_IndexesToDrop;

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexDropStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_IndexDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_IndexDropStmts (Stmt)
            SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                          GROUP_CONCAT(CONCAT('DROP INDEX `', IndexName, '`') ORDER BY IndexName SEPARATOR ', '))
            FROM _SchemaSmith_IndexesToDrop
            GROUP BY TableName;

            SET @v_indexdrop_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexDropStmts);
            WHILE @v_indexdrop_id IS NOT NULL DO
                SELECT Stmt INTO @exec_sql FROM _SchemaSmith_IndexDropStmts WHERE RowId = @v_indexdrop_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @v_indexdrop_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexDropStmts WHERE RowId > @v_indexdrop_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexDropStmts;

            -- Remove dropped indexes from ProductOwnership
            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_IndexesToDrop itd
                ON po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(itd.TableName, '.', itd.IndexName) COLLATE utf8mb4_unicode_ci
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxOnlyIdx;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxOnlyKCU;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxOnlyTC;
    END IF;

    -- =========================================================================
    -- STEP 4: Create missing indexes (non-primary)
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
                           ELSE '' END,
                      CASE WHEN i.IsVisible = 0 THEN ' INVISIBLE' ELSE '' END)
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
              WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
          );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Create index: ', i.TableName, '.', i.IndexName,
            CASE WHEN COALESCE(i.VariantName, '') <> '' THEN CONCAT(' (variant: ', i.VariantName, ')') ELSE '' END)
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
              WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
          );

        -- Fold each table's missing-index creates into one multi-clause ALTER, materialize, execute.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexCreateStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexCreateStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IndexCreateStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', i.TableName, ' ',
                      GROUP_CONCAT(
                          CONCAT(
                              'ADD ',
                              CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL INDEX '
                                   WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE INDEX '
                                   ELSE 'INDEX ' END,
                              i.IndexName,
                              ' (', i.IndexColumns, ')',
                              CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                                   WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                                   ELSE '' END,
                              CASE WHEN i.IsVisible = 0 THEN ' INVISIBLE' ELSE '' END)
                          ORDER BY i.IndexName SEPARATOR ', '))
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
              WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
          )
        GROUP BY i.TableName;

        SET @v_indexcreate_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexCreateStmts);
        WHILE @v_indexcreate_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_IndexCreateStmts WHERE RowId = @v_indexcreate_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_indexcreate_id := (SELECT MIN(RowId) FROM _SchemaSmith_IndexCreateStmts WHERE RowId > @v_indexcreate_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexCreateStmts;
    END IF;

    -- =========================================================================
    -- STEP 5: Update ProductOwnership for managed indexes
    -- =========================================================================
    IF p_WhatIf = 0 THEN
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
    END IF;

    -- =========================================================================
    -- STEP 6: Handle fulltext indexes
    -- =========================================================================
    -- Drop fulltext indexes that no longer exist in definition
    IF p_DropUnknownIndexes = 1 AND p_WhatIf = 0 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown fulltext indexes');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FTIndexesToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_FTIndexesToDrop (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_FTIndexesToDrop (TableName, IndexName)
        SELECT
            CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
            CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.INDEX_TYPE = 'FULLTEXT'
          AND s.SEQ_IN_INDEX = 1
          AND EXISTS (
              SELECT 1 FROM SchemaSmith_ProductOwnership po
              WHERE BINARY po.ProductName = BINARY p_ProductName
                AND BINARY po.ObjectSchema = BINARY p_DatabaseName
                AND po.ObjectType = 'INDEX'
                AND BINARY po.ObjectName = BINARY CONCAT(CONVERT(s.TABLE_NAME USING utf8mb4), '.', CONVERT(s.INDEX_NAME USING utf8mb4))
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_FullTextIndexes ft
              WHERE BINARY SchemaSmith_StripBacktickWrapping(ft.TableName) = BINARY s.TABLE_NAME
                AND BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName) = BINARY s.INDEX_NAME
          );

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop unknown fulltext index: DROP INDEX `', IndexName,
                      '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_FTIndexesToDrop;

        -- Fold each table's fulltext-index drops into one multi-clause ALTER, materialize, execute.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FTDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_FTDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_FTDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP INDEX `', IndexName, '`') ORDER BY IndexName SEPARATOR ', '))
        FROM _SchemaSmith_FTIndexesToDrop
        GROUP BY TableName;

        SET @v_ftdrop_id := (SELECT MIN(RowId) FROM _SchemaSmith_FTDropStmts);
        WHILE @v_ftdrop_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FTDropStmts WHERE RowId = @v_ftdrop_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_ftdrop_id := (SELECT MIN(RowId) FROM _SchemaSmith_FTDropStmts WHERE RowId > @v_ftdrop_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FTDropStmts;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FTIndexesToDrop;
    END IF;

    -- Create missing fulltext indexes
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing fulltext indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT(
                      'CREATE FULLTEXT INDEX ', ft.IndexName,
                      ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', ft.TableName,
                      ' (', ft.Columns, ')',
                      CASE WHEN ft.Comment IS NOT NULL AND ft.Comment != ''
                           THEN CONCAT(' COMMENT ''', REPLACE(ft.Comment, '''', ''''''), '''')
                           ELSE '' END,
                      CASE WHEN ft.Parser IS NOT NULL AND ft.Parser != ''
                           THEN CONCAT(' WITH PARSER ', ft.Parser)
                           ELSE '' END)
        FROM _SchemaSmith_FullTextIndexes ft
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
              AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
              AND s.INDEX_TYPE = 'FULLTEXT'
        );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing fulltext indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Create fulltext index: ', ft.TableName, '.', ft.IndexName,
            CASE WHEN COALESCE(ft.VariantName, '') <> '' THEN CONCAT(' (variant: ', ft.VariantName, ')') ELSE '' END)
        FROM _SchemaSmith_FullTextIndexes ft
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
              AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
              AND s.INDEX_TYPE = 'FULLTEXT'
        );

        -- NOTE: InnoDB only supports adding one FULLTEXT index per ALTER TABLE statement, so unlike
        -- the other loops in this file these do NOT fold multiple rows into one multi-clause ALTER —
        -- each row materializes as its own standalone ALTER TABLE ... ADD FULLTEXT INDEX statement.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FTCreateStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_FTCreateStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_FTCreateStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', ft.TableName,
                      ' ADD FULLTEXT INDEX ', ft.IndexName,
                      ' (', ft.Columns, ')',
                      CASE WHEN ft.Comment IS NOT NULL AND ft.Comment != ''
                           THEN CONCAT(' COMMENT ''', REPLACE(ft.Comment, '''', ''''''), '''')
                           ELSE '' END,
                      CASE WHEN ft.Parser IS NOT NULL AND ft.Parser != ''
                           THEN CONCAT(' WITH PARSER ', ft.Parser)
                           ELSE '' END)
        FROM _SchemaSmith_FullTextIndexes ft
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
              AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
              AND s.INDEX_TYPE = 'FULLTEXT'
        )
        ORDER BY ft.RowId;

        SET @v_ftcreate_id := (SELECT MIN(RowId) FROM _SchemaSmith_FTCreateStmts);
        WHILE @v_ftcreate_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FTCreateStmts WHERE RowId = @v_ftcreate_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_ftcreate_id := (SELECT MIN(RowId) FROM _SchemaSmith_FTCreateStmts WHERE RowId > @v_ftcreate_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FTCreateStmts;

        -- Track fulltext indexes in ProductOwnership
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'INDEX',
               CONCAT(SchemaSmith_StripBacktickWrapping(ft.TableName), '.', SchemaSmith_StripBacktickWrapping(ft.IndexName))
        FROM _SchemaSmith_FullTextIndexes ft
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
              AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
        );
    END IF;

    -- Cleanup temporary tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;

END//

DELIMITER ;
