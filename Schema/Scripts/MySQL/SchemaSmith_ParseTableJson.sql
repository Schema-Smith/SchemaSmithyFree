-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_ParseTableJson//

CREATE PROCEDURE SchemaSmith_ParseTableJson(
    IN p_DatabaseName VARCHAR(128),
    IN p_TableDefinitions LONGTEXT
)
SQL SECURITY DEFINER
BEGIN
    -- Loop counters for the JSON_EXTRACT-based shred of p_TableDefinitions (MySQL 5.7 / MariaDB 10.2
    -- compatible replacement for JSON_TABLE, which does not exist on those versions). One outer/inner
    -- counter pair per JSON_TABLE call this procedure used to make; grouped here because MySQL requires
    -- all DECLAREs at the top of the BEGIN block, before any executable statement.
    DECLARE v_TblCnt INT;
    DECLARE v_TblIdx INT;
    DECLARE v_ColOuterCnt INT;
    DECLARE v_ColOuterIdx INT;
    DECLARE v_ColInnerCnt INT;
    DECLARE v_ColInnerIdx INT;
    DECLARE v_IxOuterCnt INT;
    DECLARE v_IxOuterIdx INT;
    DECLARE v_IxInnerCnt INT;
    DECLARE v_IxInnerIdx INT;
    DECLARE v_FkOuterCnt INT;
    DECLARE v_FkOuterIdx INT;
    DECLARE v_FkInnerCnt INT;
    DECLARE v_FkInnerIdx INT;
    DECLARE v_ChkOuterCnt INT;
    DECLARE v_ChkOuterIdx INT;
    DECLARE v_ChkInnerCnt INT;
    DECLARE v_ChkInnerIdx INT;
    DECLARE v_FtOuterCnt INT;
    DECLARE v_FtOuterIdx INT;
    DECLARE v_FtInnerCnt INT;
    DECLARE v_FtInnerIdx INT;

    -- Parse JSON table definitions into temporary tables for MySQL
    -- These temp tables persist in the session and are used by
    -- SchemaSmith_MissingTableAndColumnQuench, SchemaSmith_ModifiedTableQuench, and SchemaSmith_MissingIndexesAndConstraintsQuench

    -- Drop existing temp tables if they exist
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Tables;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Columns;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Indexes;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ForeignKeys;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CheckConstraints;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FullTextIndexes;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Parse table definitions');

    -- Parse Tables from JSON
    -- RowId is the synthetic primary key so the per-row ShouldApply UPDATE below can target
    -- exactly the source row whose expression evaluated false. Without it, two table entries
    -- sharing a Name with mutually exclusive ShouldApply would either collide on the natural-key
    -- PRIMARY KEY (TableName) at INSERT time or silently mark both rows ShouldApply=0 at UPDATE
    -- time. TableName remains an indexed lookup column but is no longer the uniqueness constraint.
    CREATE TEMPORARY TABLE _SchemaSmith_Tables (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        Engine VARCHAR(50) DEFAULT 'InnoDB',
        Collation VARCHAR(100) DEFAULT NULL,
        OldName VARCHAR(128) DEFAULT NULL,
        RowFormat VARCHAR(20) DEFAULT NULL,
        AutoIncrementValue BIGINT UNSIGNED DEFAULT NULL,
        NewTable TINYINT DEFAULT 0,
        ShouldApply TINYINT DEFAULT 1,
        ShouldApplyExpression VARCHAR(4000) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        AutoIncrementKeyClause VARCHAR(500) DEFAULT '',
        DropColumnsRemovedFromProduct TINYINT DEFAULT NULL,
        DropForeignKeysRemovedFromProduct TINYINT DEFAULT NULL,
        DropCheckConstraintsRemovedFromProduct TINYINT DEFAULT NULL,
        DropIndexesRemovedFromProduct TINYINT DEFAULT NULL,
        PreventDrop TINYINT DEFAULT 0,
        KEY ix_tables_name (TableName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- First, insert all tables from JSON with NewTable = 0 (assume existing)
    -- The NOT EXISTS check against INFORMATION_SCHEMA is done separately via UPDATE
    -- to avoid a MySQL optimizer issue where correlated subqueries with function calls
    -- inside JSON_TABLE context don't re-evaluate correctly for all rows.
    SET v_TblCnt = JSON_LENGTH(p_TableDefinitions);
    SET v_TblIdx = 0;
    WHILE v_TblIdx < v_TblCnt DO
        IF SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].Name'))) IS NOT NULL THEN
            INSERT INTO _SchemaSmith_Tables (TableName, Engine, Collation, OldName, RowFormat, AutoIncrementValue, NewTable, ShouldApply, ShouldApplyExpression, VariantName, DropColumnsRemovedFromProduct, DropForeignKeysRemovedFromProduct, DropCheckConstraintsRemovedFromProduct, DropIndexesRemovedFromProduct, PreventDrop)
            SELECT
                SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].Name')))) AS TableName,
                COALESCE(NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].Engine')))), ''), 'InnoDB') AS Engine,
                NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].Collation')))), '') AS Collation,
                SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].OldName')))) AS OldName,
                NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].RowFormat')))), '') AS RowFormat,
                SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].AutoIncrementValue'))) AS AutoIncrementValue,
                0 AS NewTable,
                1 AS ShouldApply,
                NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].ShouldApplyExpression')))), '') AS ShouldApplyExpression,
                NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].VariantName')))), '') AS VariantName,
                SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].DropColumnsRemovedFromProduct'))) AS DropColumnsRemovedFromProduct,
                SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].DropForeignKeysRemovedFromProduct'))) AS DropForeignKeysRemovedFromProduct,
                SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].DropCheckConstraintsRemovedFromProduct'))) AS DropCheckConstraintsRemovedFromProduct,
                SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].DropIndexesRemovedFromProduct'))) AS DropIndexesRemovedFromProduct,
                COALESCE(SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_TblIdx, '].PreventDrop'))), 0) AS PreventDrop;
        END IF;
        SET v_TblIdx = v_TblIdx + 1;
    END WHILE;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Identify new tables');

    -- Snapshot existing tables into a temp table to avoid MySQL optimizer issues
    -- with correlated NOT EXISTS subqueries against INFORMATION_SCHEMA.
    -- The optimizer can cache/materialize INFORMATION_SCHEMA results incorrectly
    -- when used in correlated subqueries (both in JSON_TABLE and UPDATE contexts).
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingTables;
    CREATE TEMPORARY TABLE _SchemaSmith_ExistingTables (
        TableName VARCHAR(128) NOT NULL PRIMARY KEY
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO _SchemaSmith_ExistingTables (TableName)
    SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
    WHERE BINARY TABLE_SCHEMA = BINARY p_DatabaseName
    AND TABLE_TYPE = 'BASE TABLE';

    -- Now set NewTable = 1 for tables not found in snapshot
    UPDATE _SchemaSmith_Tables t
    SET t.NewTable = 1
    WHERE NOT EXISTS (
        SELECT 1 FROM _SchemaSmith_ExistingTables et
        WHERE BINARY et.TableName = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        OR (t.OldName IS NOT NULL AND BINARY et.TableName = BINARY SchemaSmith_StripBacktickWrapping(t.OldName))
    );

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingTables;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Parse columns');

    -- Parse Columns from JSON
    -- RowId is the synthetic primary key (see _SchemaSmith_Tables above). The natural key
    -- (TableName, ColumnName) becomes a regular index for lookup performance. Two same-named
    -- column entries with mutually exclusive ShouldApply expressions can now coexist in
    -- _SchemaSmith_Columns until ShouldApply DELETE removes the one whose expression evaluates false.
    CREATE TEMPORARY TABLE _SchemaSmith_Columns (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        OrdinalPosition INT NOT NULL DEFAULT 0,
        DataType VARCHAR(100) NOT NULL,
        IsNullable TINYINT DEFAULT 1,
        DefaultValue TEXT DEFAULT NULL,
        IsAutoIncrement TINYINT DEFAULT 0,
        GeneratedExpression TEXT DEFAULT NULL,
        GeneratedType VARCHAR(10) DEFAULT NULL,
        CharacterSet VARCHAR(50) DEFAULT NULL,
        Collation VARCHAR(100) DEFAULT NULL,
        CheckExpression TEXT DEFAULT NULL,
        OldName VARCHAR(128) DEFAULT NULL,
        NewColumn TINYINT DEFAULT 0,
        ColumnScript TEXT DEFAULT NULL,
        DependencyLevel INT DEFAULT 0,
        ShouldApply TINYINT DEFAULT 1,
        ShouldApplyExpression VARCHAR(4000) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_columns_table_name (TableName, ColumnName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    SET v_ColOuterCnt = JSON_LENGTH(p_TableDefinitions);
    SET v_ColOuterIdx = 0;
    WHILE v_ColOuterIdx < v_ColOuterCnt DO
        SET v_ColInnerCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns'))), 0);
        SET v_ColInnerIdx = 0;
        WHILE v_ColInnerIdx < v_ColInnerCnt DO
            IF SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].Name'))) IS NOT NULL
               AND EXISTS (SELECT 1 FROM _SchemaSmith_Tables st WHERE st.TableName = SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Name'))))) THEN
                INSERT INTO _SchemaSmith_Columns (
                    TableName, ColumnName, OrdinalPosition, DataType, IsNullable, DefaultValue,
                    IsAutoIncrement, GeneratedExpression, GeneratedType,
                    CharacterSet, Collation, CheckExpression, OldName, NewColumn, ShouldApply, ShouldApplyExpression, VariantName
                )
                SELECT
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Name')))) AS TableName,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].Name')))) AS ColumnName,
                    v_ColInnerIdx + 1 AS OrdinalPosition,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].DataType'))) AS DataType,
                    COALESCE(SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].Nullable'))), 1) AS IsNullable,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].Default'))) AS DefaultValue,
                    COALESCE(SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].AutoIncrement'))), 0) AS IsAutoIncrement,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].GenerationExpression'))) AS GeneratedExpression,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].Generated'))) AS GeneratedType,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].CharacterSet'))) AS CharacterSet,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].Collation'))) AS Collation,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].CheckExpression'))) AS CheckExpression,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].OldName')))) AS OldName,
                    0 AS NewColumn,
                    1 AS ShouldApply,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].ShouldApplyExpression')))), '') AS ShouldApplyExpression,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ColOuterIdx, '].Columns[', v_ColInnerIdx, '].VariantName')))), '') AS VariantName;
            END IF;
            SET v_ColInnerIdx = v_ColInnerIdx + 1;
        END WHILE;
        SET v_ColOuterIdx = v_ColOuterIdx + 1;
    END WHILE;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Identify new columns');

    -- Snapshot existing columns into a temp table (same optimizer workaround as tables)
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingColumns;
    CREATE TEMPORARY TABLE _SchemaSmith_ExistingColumns (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO _SchemaSmith_ExistingColumns (TableName, ColumnName)
    SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
    WHERE BINARY TABLE_SCHEMA = BINARY p_DatabaseName;

    -- Now set NewColumn = 1 for columns not found in snapshot
    UPDATE _SchemaSmith_Columns c
    SET c.NewColumn = 1
    WHERE NOT EXISTS (
        SELECT 1 FROM _SchemaSmith_ExistingColumns ec
        WHERE BINARY ec.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
        AND (
            BINARY ec.ColumnName = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
            OR (c.OldName IS NOT NULL AND BINARY ec.ColumnName = BINARY SchemaSmith_StripBacktickWrapping(c.OldName))
        )
    );

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingColumns;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Build column scripts');

    -- Build ColumnScript for each column
    UPDATE _SchemaSmith_Columns
    SET ColumnScript = CONCAT(
        ColumnName, ' ',
        CASE
            WHEN GeneratedExpression IS NOT NULL AND TRIM(GeneratedExpression) != '' THEN
                CONCAT(
                    SchemaSmith_UpperDataType(DataType), ' ',
                    'GENERATED ALWAYS AS (', GeneratedExpression, ') ',
                    COALESCE(UPPER(GeneratedType), 'VIRTUAL')
                )
            ELSE
                CONCAT(
                    SchemaSmith_UpperDataType(DataType),
                    CASE WHEN CharacterSet IS NOT NULL AND TRIM(CharacterSet) != ''
                         THEN CONCAT(' CHARACTER SET ', CharacterSet) ELSE '' END,
                    CASE WHEN Collation IS NOT NULL AND TRIM(Collation) != ''
                         THEN CONCAT(' COLLATE ', Collation) ELSE '' END,
                    CASE WHEN IsNullable = 1 THEN ' NULL' ELSE ' NOT NULL' END,
                    CASE WHEN IsAutoIncrement = 1 THEN ' AUTO_INCREMENT' ELSE '' END,
                    CASE WHEN DefaultValue IS NOT NULL AND TRIM(DefaultValue) != '' AND IsAutoIncrement = 0
                         THEN CONCAT(' DEFAULT ',
                              CASE WHEN DefaultValue REGEXP '\\(' AND LEFT(DefaultValue, 1) != '('
                                   THEN CONCAT('(', DefaultValue, ')')
                                   ELSE DefaultValue END)
                         ELSE '' END
                )
        END
    );

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Calculate generated column dependencies');

    -- Calculate DependencyLevel for generated columns
    -- Level 0: Regular columns (default)
    -- Level 1+: Generated columns, ordered by dependencies on other generated columns
    --
    -- Note: MySQL doesn't allow reopening a temp table in any self-referential context,
    -- so we use TWO separate temp tables and copy between them.

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDeps;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDeps2;

    CREATE TEMPORARY TABLE _SchemaSmith_GenColDeps (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        ColumnNameStripped VARCHAR(128) NOT NULL,
        GeneratedExpression TEXT NOT NULL,
        DependencyLevel INT DEFAULT 0,
        PRIMARY KEY (TableName, ColumnName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    CREATE TEMPORARY TABLE _SchemaSmith_GenColDeps2 (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        ColumnNameStripped VARCHAR(128) NOT NULL,
        GeneratedExpression TEXT NOT NULL,
        DependencyLevel INT DEFAULT 0,
        PRIMARY KEY (TableName, ColumnName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Copy generated columns to first helper table
    INSERT INTO _SchemaSmith_GenColDeps (TableName, ColumnName, ColumnNameStripped, GeneratedExpression)
    SELECT TableName, ColumnName, SchemaSmith_StripBacktickWrapping(ColumnName), GeneratedExpression
    FROM _SchemaSmith_Columns
    WHERE GeneratedExpression IS NOT NULL
      AND TRIM(GeneratedExpression) != '';

    -- Make a copy for self-reference queries
    INSERT INTO _SchemaSmith_GenColDeps2 SELECT * FROM _SchemaSmith_GenColDeps;

    -- Iteratively assign dependency levels
    SET @_ssc_curr_level = 0;
    SET @_ssc_prev_unresolved = -1;

    dep_loop: WHILE @_ssc_curr_level < 10 DO
        SET @_ssc_curr_level = @_ssc_curr_level + 1;

        -- Count unresolved generated columns
        SELECT COUNT(*) INTO @_ssc_curr_unresolved
        FROM _SchemaSmith_GenColDeps
        WHERE DependencyLevel = 0;

        -- If no change from last iteration, we're either done or have a cycle
        IF @_ssc_curr_unresolved = @_ssc_prev_unresolved THEN
            LEAVE dep_loop;
        END IF;
        SET @_ssc_prev_unresolved = @_ssc_curr_unresolved;

        -- If nothing left to resolve, we're done
        IF @_ssc_curr_unresolved = 0 THEN
            LEAVE dep_loop;
        END IF;

        -- Update columns in GenColDeps whose dependencies (checked in GenColDeps2) are all resolved
        -- A column can be resolved if no other unresolved generated column is referenced in its expression
        UPDATE _SchemaSmith_GenColDeps g1
        SET g1.DependencyLevel = @_ssc_curr_level
        WHERE g1.DependencyLevel = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_GenColDeps2 g2
              WHERE g2.TableName = g1.TableName
                AND g2.DependencyLevel = 0
                AND g2.ColumnName != g1.ColumnName
                AND (g1.GeneratedExpression LIKE CONCAT('%', g2.ColumnNameStripped, '%')
                     OR g1.GeneratedExpression LIKE CONCAT('%', g2.ColumnName, '%'))
          );

        -- Sync the changes back to GenColDeps2
        UPDATE _SchemaSmith_GenColDeps2 g2
        INNER JOIN _SchemaSmith_GenColDeps g1
            ON g2.TableName = g1.TableName AND g2.ColumnName = g1.ColumnName
        SET g2.DependencyLevel = g1.DependencyLevel;
    END WHILE;

    -- Check for circular dependencies (generated columns still at level 0 after all iterations)
    IF EXISTS (
        SELECT 1 FROM _SchemaSmith_GenColDeps
        WHERE DependencyLevel = 0
    ) THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDeps;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDeps2;
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Circular dependency detected in generated columns. Cannot determine creation order.';
    END IF;

    -- Copy dependency levels back to main columns table
    UPDATE _SchemaSmith_Columns c
    INNER JOIN _SchemaSmith_GenColDeps g
        ON c.TableName = g.TableName AND c.ColumnName = g.ColumnName
    SET c.DependencyLevel = g.DependencyLevel;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDeps;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDeps2;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Parse indexes');

    -- Parse Indexes from JSON
    -- RowId is the synthetic primary key (see _SchemaSmith_Tables above for rationale).
    CREATE TEMPORARY TABLE _SchemaSmith_Indexes (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        IsPrimaryKey TINYINT DEFAULT 0,
        IsUnique TINYINT DEFAULT 0,
        IndexType VARCHAR(20) DEFAULT 'BTREE',
        IndexColumns TEXT NOT NULL,
        IsVisible TINYINT DEFAULT 1,
        ShouldApply TINYINT DEFAULT 1,
        ShouldApplyExpression VARCHAR(4000) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_indexes_table_name (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    SET v_IxOuterCnt = JSON_LENGTH(p_TableDefinitions);
    SET v_IxOuterIdx = 0;
    WHILE v_IxOuterIdx < v_IxOuterCnt DO
        SET v_IxInnerCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes'))), 0);
        SET v_IxInnerIdx = 0;
        WHILE v_IxInnerIdx < v_IxInnerCnt DO
            IF SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].Name'))) IS NOT NULL
               AND EXISTS (SELECT 1 FROM _SchemaSmith_Tables st WHERE st.TableName = SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Name'))))) THEN
                INSERT INTO _SchemaSmith_Indexes (
                    TableName, IndexName, IsPrimaryKey, IsUnique, IndexType, IndexColumns, IsVisible, ShouldApply, ShouldApplyExpression, VariantName
                )
                SELECT
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Name')))) AS TableName,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].Name')))) AS IndexName,
                    COALESCE(SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].PrimaryKey'))), 0) AS IsPrimaryKey,
                    COALESCE(SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].Unique'))), SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].PrimaryKey'))), 0) AS IsUnique,
                    COALESCE(NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].IndexType')))), ''), 'BTREE') AS IndexType,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].IndexColumns'))) AS IndexColumns,
                    COALESCE(SchemaSmith_JsonScalarInt(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].Visible'))), 1) AS IsVisible,
                    1 AS ShouldApply,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].ShouldApplyExpression')))), '') AS ShouldApplyExpression,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_IxOuterIdx, '].Indexes[', v_IxInnerIdx, '].VariantName')))), '') AS VariantName;
            END IF;
            SET v_IxInnerIdx = v_IxInnerIdx + 1;
        END WHILE;
        SET v_IxOuterIdx = v_IxOuterIdx + 1;
    END WHILE;

    -- MySQL: AUTO_INCREMENT column must be indexed. When it's not the first column
    -- in a composite PK, we need a separate KEY clause in the CREATE TABLE statement.
    -- Uses JOIN syntax instead of correlated subquery to avoid MySQL temp table re-open restriction.
    UPDATE _SchemaSmith_Tables t
      JOIN _SchemaSmith_Columns c ON c.TableName = t.TableName AND c.IsAutoIncrement = 1
      JOIN _SchemaSmith_Indexes i ON i.TableName = t.TableName AND i.IsPrimaryKey = 1
       SET t.AutoIncrementKeyClause = CONCAT(', KEY (`', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`)')
     WHERE LOCATE(CONCAT('`', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`') COLLATE utf8mb4_unicode_ci, i.IndexColumns COLLATE utf8mb4_unicode_ci) > 1;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Parse foreign keys');

    -- Parse Foreign Keys from JSON
    -- RowId is the synthetic primary key (see _SchemaSmith_Tables above for rationale).
    CREATE TEMPORARY TABLE _SchemaSmith_ForeignKeys (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        KeyName VARCHAR(128) NOT NULL,
        Columns TEXT NOT NULL,
        RelatedTableSchema VARCHAR(128) DEFAULT NULL,
        RelatedTable VARCHAR(128) NOT NULL,
        RelatedColumns TEXT NOT NULL,
        DeleteAction VARCHAR(20) DEFAULT 'NO ACTION',
        UpdateAction VARCHAR(20) DEFAULT 'NO ACTION',
        ShouldApply TINYINT DEFAULT 1,
        ShouldApplyExpression VARCHAR(4000) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_fks_table_name (TableName, KeyName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    SET v_FkOuterCnt = JSON_LENGTH(p_TableDefinitions);
    SET v_FkOuterIdx = 0;
    WHILE v_FkOuterIdx < v_FkOuterCnt DO
        SET v_FkInnerCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys'))), 0);
        SET v_FkInnerIdx = 0;
        WHILE v_FkInnerIdx < v_FkInnerCnt DO
            IF SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].Name'))) IS NOT NULL
               AND EXISTS (SELECT 1 FROM _SchemaSmith_Tables st WHERE st.TableName = SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].Name'))))) THEN
                INSERT INTO _SchemaSmith_ForeignKeys (
                    TableName, KeyName, Columns, RelatedTableSchema, RelatedTable, RelatedColumns, DeleteAction, UpdateAction, ShouldApply, ShouldApplyExpression, VariantName
                )
                SELECT
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].Name')))) AS TableName,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].Name')))) AS KeyName,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].Columns'))) AS Columns,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].RelatedTableSchema')))), '') AS RelatedTableSchema,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].RelatedTable')))) AS RelatedTable,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].RelatedColumns'))) AS RelatedColumns,
                    COALESCE(NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].DeleteAction')))), ''), 'NO ACTION') AS DeleteAction,
                    COALESCE(NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].UpdateAction')))), ''), 'NO ACTION') AS UpdateAction,
                    1 AS ShouldApply,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].ShouldApplyExpression')))), '') AS ShouldApplyExpression,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FkOuterIdx, '].ForeignKeys[', v_FkInnerIdx, '].VariantName')))), '') AS VariantName;
            END IF;
            SET v_FkInnerIdx = v_FkInnerIdx + 1;
        END WHILE;
        SET v_FkOuterIdx = v_FkOuterIdx + 1;
    END WHILE;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Parse check constraints');

    -- Parse Check Constraints from JSON (MySQL 8.0.16+)
    -- RowId is the synthetic primary key (see _SchemaSmith_Tables above for rationale).
    CREATE TEMPORARY TABLE _SchemaSmith_CheckConstraints (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        ConstraintName VARCHAR(128) NOT NULL,
        Expression TEXT NOT NULL,
        ShouldApply TINYINT DEFAULT 1,
        ShouldApplyExpression VARCHAR(4000) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_checks_table_name (TableName, ConstraintName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    SET v_ChkOuterCnt = JSON_LENGTH(p_TableDefinitions);
    SET v_ChkOuterIdx = 0;
    WHILE v_ChkOuterIdx < v_ChkOuterCnt DO
        SET v_ChkInnerCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].CheckConstraints'))), 0);
        SET v_ChkInnerIdx = 0;
        WHILE v_ChkInnerIdx < v_ChkInnerCnt DO
            IF SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].CheckConstraints[', v_ChkInnerIdx, '].Name'))) IS NOT NULL
               AND EXISTS (SELECT 1 FROM _SchemaSmith_Tables st WHERE st.TableName = SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].Name'))))) THEN
                INSERT INTO _SchemaSmith_CheckConstraints (TableName, ConstraintName, Expression, ShouldApply, ShouldApplyExpression, VariantName)
                SELECT
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].Name')))) AS TableName,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].CheckConstraints[', v_ChkInnerIdx, '].Name')))) AS ConstraintName,
                    -- Strip MySQL charset introducers (_utf8mb4, _utf8, etc.) and backslash-escaped quotes
                    -- from CHECK_CLAUSE expressions. MySQL's INFORMATION_SCHEMA stores these internally
                    -- but they're not needed in DDL and cause PREPARE failures.
                    REPLACE(
                        REGEXP_REPLACE(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].CheckConstraints[', v_ChkInnerIdx, '].Expression'))), '_utf8mb4|_utf8|_latin1|_binary', ''),
                        '\\''', '''') AS Expression,
                    1 AS ShouldApply,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].CheckConstraints[', v_ChkInnerIdx, '].ShouldApplyExpression')))), '') AS ShouldApplyExpression,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_ChkOuterIdx, '].CheckConstraints[', v_ChkInnerIdx, '].VariantName')))), '') AS VariantName;
            END IF;
            SET v_ChkInnerIdx = v_ChkInnerIdx + 1;
        END WHILE;
        SET v_ChkOuterIdx = v_ChkOuterIdx + 1;
    END WHILE;

    -- Note: Tables with just a Name and no columns/indexes are valid for IndexOnlyQuench
    -- scenarios where we want to track that the table is "in the definition" for the purpose
    -- of managing product-owned indexes. All tables in _SchemaSmith_Tables came from the JSON
    -- so they were explicitly included by the user.

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Parse fulltext indexes');

    -- Parse FullText Indexes from JSON
    -- The temp table schema MUST match IndexOnlyQuench's fallback definition exactly
    -- (IndexOnlyQuench.sql has the same CREATE TEMPORARY TABLE IF NOT EXISTS as a fallback;
    -- keep them in lockstep when changing either definition). The earlier helper-temp-table
    -- approach (_SchemaSmith_FTShouldApply) for ShouldApply evaluation was retired so this
    -- table follows the same RowId + ShouldApply+ShouldApplyExpression pattern as the others.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FullTextIndexes;
    CREATE TEMPORARY TABLE IF NOT EXISTS _SchemaSmith_FullTextIndexes (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        Columns TEXT NOT NULL,
        Parser VARCHAR(128) DEFAULT NULL,
        Comment VARCHAR(255) DEFAULT NULL,
        ShouldApply TINYINT DEFAULT 1,
        ShouldApplyExpression VARCHAR(4000) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_ft_table_name (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    SET v_FtOuterCnt = JSON_LENGTH(p_TableDefinitions);
    SET v_FtOuterIdx = 0;
    WHILE v_FtOuterIdx < v_FtOuterCnt DO
        SET v_FtInnerCnt = COALESCE(JSON_LENGTH(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes'))), 0);
        SET v_FtInnerIdx = 0;
        WHILE v_FtInnerIdx < v_FtInnerCnt DO
            IF SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].Name'))) IS NOT NULL
               AND EXISTS (SELECT 1 FROM _SchemaSmith_Tables st WHERE st.TableName = SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].Name'))))) THEN
                INSERT INTO _SchemaSmith_FullTextIndexes (TableName, IndexName, Columns, Parser, Comment, ShouldApply, ShouldApplyExpression, VariantName)
                SELECT
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].Name')))) AS TableName,
                    SchemaSmith_SafeBacktickWrap(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].Name')))) AS IndexName,
                    SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].Columns'))) AS Columns,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].Parser')))), '') AS Parser,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].Comment')))), '') AS Comment,
                    1 AS ShouldApply,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].ShouldApplyExpression')))), '') AS ShouldApplyExpression,
                    NULLIF(TRIM(SchemaSmith_JsonScalarStr(JSON_EXTRACT(p_TableDefinitions, CONCAT('$[', v_FtOuterIdx, '].FullTextIndexes[', v_FtInnerIdx, '].VariantName')))), '') AS VariantName;
            END IF;
            SET v_FtInnerIdx = v_FtInnerIdx + 1;
        END WHILE;
        SET v_FtOuterIdx = v_FtOuterIdx + 1;
    END WHILE;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Evaluate ShouldApplyExpression');

    -- Evaluate ShouldApplyExpression using dynamic SQL (matching SQL Server/PostgreSQL pattern)
    -- Build UPDATE statements into a helper table, then PREPARE/EXECUTE each one
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ShouldApplyEval;
    CREATE TEMPORARY TABLE _SchemaSmith_ShouldApplyEval (
        EvalId INT AUTO_INCREMENT PRIMARY KEY,
        EvalSql TEXT NOT NULL
    ) ENGINE=InnoDB;

    -- Tables: UPDATE ... SET ShouldApply = 0 WHERE NOT (expression)
    -- Scoped by RowId so each generated UPDATE targets exactly the source row whose expression
    -- evaluated false (no collateral damage to siblings sharing the natural-key name).
    INSERT INTO _SchemaSmith_ShouldApplyEval (EvalSql)
    SELECT CONCAT('UPDATE _SchemaSmith_Tables SET ShouldApply = 0 WHERE RowId = ',
                  RowId,
                  ' AND NOT (', SchemaSmith_StripLeadingSelect(ShouldApplyExpression), ')')
    FROM _SchemaSmith_Tables
    WHERE ShouldApplyExpression IS NOT NULL AND TRIM(ShouldApplyExpression) <> '';

    -- Columns: UPDATE ... SET ShouldApply = 0 WHERE NOT (expression) (scoped by RowId)
    INSERT INTO _SchemaSmith_ShouldApplyEval (EvalSql)
    SELECT CONCAT('UPDATE _SchemaSmith_Columns SET ShouldApply = 0 WHERE RowId = ',
                  RowId,
                  ' AND NOT (', SchemaSmith_StripLeadingSelect(ShouldApplyExpression), ')')
    FROM _SchemaSmith_Columns
    WHERE ShouldApplyExpression IS NOT NULL AND TRIM(ShouldApplyExpression) <> '';

    -- Indexes: UPDATE ... SET ShouldApply = 0 WHERE NOT (expression) (scoped by RowId)
    INSERT INTO _SchemaSmith_ShouldApplyEval (EvalSql)
    SELECT CONCAT('UPDATE _SchemaSmith_Indexes SET ShouldApply = 0 WHERE RowId = ',
                  RowId,
                  ' AND NOT (', SchemaSmith_StripLeadingSelect(ShouldApplyExpression), ')')
    FROM _SchemaSmith_Indexes
    WHERE ShouldApplyExpression IS NOT NULL AND TRIM(ShouldApplyExpression) <> '';

    -- ForeignKeys: UPDATE ... SET ShouldApply = 0 WHERE NOT (expression) (scoped by RowId)
    INSERT INTO _SchemaSmith_ShouldApplyEval (EvalSql)
    SELECT CONCAT('UPDATE _SchemaSmith_ForeignKeys SET ShouldApply = 0 WHERE RowId = ',
                  RowId,
                  ' AND NOT (', SchemaSmith_StripLeadingSelect(ShouldApplyExpression), ')')
    FROM _SchemaSmith_ForeignKeys
    WHERE ShouldApplyExpression IS NOT NULL AND TRIM(ShouldApplyExpression) <> '';

    -- CheckConstraints: UPDATE ... SET ShouldApply = 0 WHERE NOT (expression) (scoped by RowId)
    INSERT INTO _SchemaSmith_ShouldApplyEval (EvalSql)
    SELECT CONCAT('UPDATE _SchemaSmith_CheckConstraints SET ShouldApply = 0 WHERE RowId = ',
                  RowId,
                  ' AND NOT (', SchemaSmith_StripLeadingSelect(ShouldApplyExpression), ')')
    FROM _SchemaSmith_CheckConstraints
    WHERE ShouldApplyExpression IS NOT NULL AND TRIM(ShouldApplyExpression) <> '';

    -- FullTextIndexes: UPDATE ... SET ShouldApply = 0 WHERE NOT (expression) (scoped by RowId)
    -- ShouldApply / ShouldApplyExpression now live directly on _SchemaSmith_FullTextIndexes
    -- (the IndexOnlyQuench fallback schema was updated in lockstep), matching the pattern of
    -- the other parser temp tables. The earlier _SchemaSmith_FTShouldApply helper table has been
    -- retired -- it was a workaround for the missing columns and is no longer needed.
    INSERT INTO _SchemaSmith_ShouldApplyEval (EvalSql)
    SELECT CONCAT('UPDATE _SchemaSmith_FullTextIndexes SET ShouldApply = 0 WHERE RowId = ',
                  RowId,
                  ' AND NOT (', SchemaSmith_StripLeadingSelect(ShouldApplyExpression), ')')
    FROM _SchemaSmith_FullTextIndexes
    WHERE ShouldApplyExpression IS NOT NULL AND TRIM(ShouldApplyExpression) <> '';

    -- Execute each statement via PREPARE/EXECUTE loop
    SET @_ssa_eval_id = 0;
    ssa_eval_loop: LOOP
        SELECT EvalId, EvalSql INTO @_ssa_eval_id, @_ssa_eval_sql
        FROM _SchemaSmith_ShouldApplyEval WHERE EvalId > @_ssa_eval_id
        ORDER BY EvalId LIMIT 1;
        IF ROW_COUNT() = 0 THEN LEAVE ssa_eval_loop; END IF;
        PREPARE _ssa_stmt FROM @_ssa_eval_sql;
        EXECUTE _ssa_stmt;
        DEALLOCATE PREPARE _ssa_stmt;
    END LOOP;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ShouldApplyEval;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'ParseTableJson: Apply conditional filters');

    -- Filter out objects where ShouldApply = 0 (conditional deployment)
    -- Note: Table-level ShouldApply=0 removes the entire table and all its objects
    DELETE FROM _SchemaSmith_CheckConstraints WHERE ShouldApply = 0;
    DELETE FROM _SchemaSmith_ForeignKeys WHERE ShouldApply = 0;
    DELETE FROM _SchemaSmith_Indexes WHERE ShouldApply = 0;
    DELETE FROM _SchemaSmith_FullTextIndexes WHERE ShouldApply = 0;
    DELETE FROM _SchemaSmith_Columns WHERE ShouldApply = 0;

    -- Now that ShouldApply has filtered each temp table down to at most one row per natural
    -- key, promote the natural-key indexes to UNIQUE so MySQL's optimizer can infer functional
    -- dependency in downstream GROUP BY queries (ONLY_FULL_GROUP_BY mode is the default in 8.0+).
    -- These indexes existed as regular KEYs during the INSERT/UPDATE phases so two same-named
    -- rows with mutually exclusive ShouldApplyExpression could coexist until the DELETE pass.
    ALTER TABLE _SchemaSmith_Tables DROP KEY ix_tables_name, ADD UNIQUE KEY uq_tables_name (TableName);
    ALTER TABLE _SchemaSmith_Columns DROP KEY ix_columns_table_name, ADD UNIQUE KEY uq_columns_table_name (TableName, ColumnName);
    ALTER TABLE _SchemaSmith_Indexes DROP KEY ix_indexes_table_name, ADD UNIQUE KEY uq_indexes_table_name (TableName, IndexName);
    ALTER TABLE _SchemaSmith_ForeignKeys DROP KEY ix_fks_table_name, ADD UNIQUE KEY uq_fks_table_name (TableName, KeyName);
    ALTER TABLE _SchemaSmith_CheckConstraints DROP KEY ix_checks_table_name, ADD UNIQUE KEY uq_checks_table_name (TableName, ConstraintName);
    ALTER TABLE _SchemaSmith_FullTextIndexes DROP KEY ix_ft_table_name, ADD UNIQUE KEY uq_ft_table_name (TableName, IndexName);

    -- Delete objects belonging to tables that should not apply
    DELETE cc FROM _SchemaSmith_CheckConstraints cc
    INNER JOIN _SchemaSmith_Tables t ON cc.TableName = t.TableName
    WHERE t.ShouldApply = 0;

    DELETE fk FROM _SchemaSmith_ForeignKeys fk
    INNER JOIN _SchemaSmith_Tables t ON fk.TableName = t.TableName
    WHERE t.ShouldApply = 0;

    DELETE i FROM _SchemaSmith_Indexes i
    INNER JOIN _SchemaSmith_Tables t ON i.TableName = t.TableName
    WHERE t.ShouldApply = 0;

    DELETE ft FROM _SchemaSmith_FullTextIndexes ft
    INNER JOIN _SchemaSmith_Tables t ON ft.TableName = t.TableName
    WHERE t.ShouldApply = 0;

    DELETE c FROM _SchemaSmith_Columns c
    INNER JOIN _SchemaSmith_Tables t ON c.TableName = t.TableName
    WHERE t.ShouldApply = 0;

    DELETE FROM _SchemaSmith_Tables WHERE ShouldApply = 0;

END//

DELIMITER ;
