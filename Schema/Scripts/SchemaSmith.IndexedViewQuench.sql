-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR ALTER PROCEDURE [SchemaSmith].[IndexedViewQuench]
    @ProductName NVARCHAR(200),
    @IndexedViewSchema NVARCHAR(MAX),
    @WhatIf BIT = 0,
    @UpdateFillFactor BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Parse indexed views from JSON using OPENJSON WITH for NVARCHAR(MAX) support
    DECLARE @Views TABLE (
        ViewSchema NVARCHAR(200),
        ViewName NVARCHAR(200),
        Definition NVARCHAR(MAX),
        IndexJson NVARCHAR(MAX)
    );

    INSERT INTO @Views (ViewSchema, ViewName, Definition, IndexJson)
    SELECT
        [SchemaSmith].[fn_StripBracketWrapping](v.[Schema]),
        [SchemaSmith].[fn_StripBracketWrapping](v.[Name]),
        v.[Definition],
        v.[Indexes]
    FROM OPENJSON(@IndexedViewSchema)
    WITH (
        [Schema] NVARCHAR(200) '$.Schema',
        [Name] NVARCHAR(200) '$.Name',
        [Definition] NVARCHAR(MAX) '$.Definition',
        [Indexes] NVARCHAR(MAX) '$.Indexes' AS JSON
    ) v;

    -- Validate ownership: fail if any requested views are owned by a different product
    -- (mirrors PostgreSQL ValidateMaterializedViewOwnership pattern)
    DECLARE @conflictMsg NVARCHAR(MAX);
    SELECT @conflictMsg = STRING_AGG(
        N'Indexed view ' + s.name + N'.' + v.name + N' owned by different product. ['
        + CAST(ep.value AS NVARCHAR(200)) + N'] <> [' + @ProductName + N']', CHAR(10))
    FROM @Views rv
    INNER JOIN sys.schemas s ON s.name = rv.ViewSchema
    INNER JOIN sys.views v ON v.name = rv.ViewName AND v.schema_id = s.schema_id
    INNER JOIN sys.extended_properties ep ON ep.major_id = v.object_id AND ep.minor_id = 0
        AND ep.name = 'SchemaSmith_Product'
    WHERE CAST(ep.value AS NVARCHAR(200)) <> @ProductName;

    IF @conflictMsg IS NOT NULL
    BEGIN
        EXEC [SchemaSmith].[PrintWithNoWait] @conflictMsg;
        THROW 50001, 'One or more indexed views in this quench are already owned by another product', 1;
    END;

    -- Identify existing indexed views owned by this product
    DECLARE @ExistingViews TABLE (
        SchemaName NVARCHAR(200),
        ViewName NVARCHAR(200),
        ObjectId INT,
        CurrentDefinition NVARCHAR(MAX)
    );

    INSERT INTO @ExistingViews
    SELECT s.name, v.name, v.object_id, m.definition
    FROM sys.views v
    INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
    INNER JOIN sys.sql_modules m ON v.object_id = m.object_id
    WHERE OBJECTPROPERTY(v.object_id, 'IsIndexed') = 1
    AND EXISTS (
        SELECT 1 FROM sys.extended_properties ep
        WHERE ep.major_id = v.object_id AND ep.minor_id = 0
        AND ep.name = 'SchemaSmith_Product' AND CAST(ep.value AS NVARCHAR(200)) = @ProductName
    );

    -- Process removals: views owned by product but not in new schema
    DECLARE @objectId INT, @schemaName NVARCHAR(200), @viewName NVARCHAR(200);
    DECLARE @sql NVARCHAR(MAX), @msg NVARCHAR(MAX);

    DECLARE remove_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT e.ObjectId, e.SchemaName, e.ViewName
        FROM @ExistingViews e
        WHERE NOT EXISTS (SELECT 1 FROM @Views v WHERE v.ViewSchema = e.SchemaName AND v.ViewName = e.ViewName);

    OPEN remove_cursor;
    FETCH NEXT FROM remove_cursor INTO @objectId, @schemaName, @viewName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @msg = N'Dropping removed indexed view ' + @schemaName + N'.' + @viewName;
        EXEC [SchemaSmith].[PrintWithNoWait] @msg;
        -- Drop nonclustered indexes first
        DECLARE @ncDropSql NVARCHAR(MAX);
        SELECT @ncDropSql = STRING_AGG('DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName), '; ')
        FROM sys.indexes i WHERE i.object_id = @objectId AND i.type > 1;
        IF @ncDropSql IS NOT NULL AND @WhatIf = 0 EXEC sp_executesql @ncDropSql;

        -- Drop clustered index
        DECLARE @clDropSql NVARCHAR(MAX);
        SELECT @clDropSql = 'DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName)
        FROM sys.indexes i WHERE i.object_id = @objectId AND i.type = 1;
        IF @clDropSql IS NOT NULL AND @WhatIf = 0 EXEC sp_executesql @clDropSql;

        -- Drop view
        SET @sql = 'DROP VIEW ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName);
        IF @WhatIf = 0 EXEC sp_executesql @sql;

        FETCH NEXT FROM remove_cursor INTO @objectId, @schemaName, @viewName;
    END;
    CLOSE remove_cursor;
    DEALLOCATE remove_cursor;

    -- Process new and changed views
    DECLARE @defn NVARCHAR(MAX), @indexJson NVARCHAR(MAX);
    DECLARE @existingDef NVARCHAR(MAX), @existingObjectId INT;
    DECLARE @needsRecreate BIT, @needsIndexUpdate BIT;

    DECLARE view_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT v.ViewSchema, v.ViewName, v.Definition, v.IndexJson FROM @Views v;

    OPEN view_cursor;
    FETCH NEXT FROM view_cursor INTO @schemaName, @viewName, @defn, @indexJson;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @needsRecreate = 0;
        SET @needsIndexUpdate = 0;
        SET @existingObjectId = NULL;

        -- Check if view exists
        SELECT @existingObjectId = e.ObjectId, @existingDef = e.CurrentDefinition
        FROM @ExistingViews e
        WHERE e.SchemaName = @schemaName AND e.ViewName = @viewName;

        IF @existingObjectId IS NOT NULL
        BEGIN
            -- Compare definitions (normalize whitespace, case-insensitive)
            DECLARE @normNew NVARCHAR(MAX) = REPLACE(REPLACE(REPLACE(LOWER(LTRIM(RTRIM(@defn))), CHAR(13), ''), CHAR(10), ' '), '  ', ' ');
            -- Extract SELECT from existing full definition
            DECLARE @existingSelect NVARCHAR(MAX);
            DECLARE @bindPos INT = CHARINDEX('SCHEMABINDING', UPPER(@existingDef));
            IF @bindPos > 0
            BEGIN
                DECLARE @afterBind NVARCHAR(MAX) = SUBSTRING(@existingDef, @bindPos + 13, LEN(@existingDef));
                DECLARE @asP INT = PATINDEX('%[^A-Za-z_]AS[^A-Za-z_]%', @afterBind);
                IF @asP > 0
                    SET @existingSelect = LTRIM(RTRIM(SUBSTRING(@afterBind, @asP + 3, LEN(@afterBind))));
                ELSE
                    SET @existingSelect = @existingDef;
            END
            ELSE
                SET @existingSelect = @existingDef;

            DECLARE @normExisting NVARCHAR(MAX) = REPLACE(REPLACE(REPLACE(LOWER(LTRIM(RTRIM(@existingSelect))), CHAR(13), ''), CHAR(10), ' '), '  ', ' ');

            IF @normNew != @normExisting
            BEGIN
                SET @needsRecreate = 1;
                -- Drop existing: nonclustered → clustered → view
                SET @msg = N'Definition changed for indexed view ' + @schemaName + N'.' + @viewName + N' — dropping for recreation';
                EXEC [SchemaSmith].[PrintWithNoWait] @msg;

                SELECT @ncDropSql = STRING_AGG('DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName), '; ')
                FROM sys.indexes i WHERE i.object_id = @existingObjectId AND i.type > 1;
                IF @ncDropSql IS NOT NULL AND @WhatIf = 0 EXEC sp_executesql @ncDropSql;

                SELECT @clDropSql = 'DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName)
                FROM sys.indexes i WHERE i.object_id = @existingObjectId AND i.type = 1;
                IF @clDropSql IS NOT NULL AND @WhatIf = 0 EXEC sp_executesql @clDropSql;

                SET @sql = 'DROP VIEW ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName);
                IF @WhatIf = 0 EXEC sp_executesql @sql;
            END
            ELSE
                SET @needsIndexUpdate = 1; -- Definition unchanged, check indexes
        END
        ELSE
            SET @needsRecreate = 1; -- New view

        -- Create view if needed
        IF @needsRecreate = 1
        BEGIN
            SET @sql = 'CREATE VIEW ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName) + ' WITH SCHEMABINDING AS ' + @defn;
            SET @msg = N'Creating indexed view ' + @schemaName + N'.' + @viewName;
            EXEC [SchemaSmith].[PrintWithNoWait] @msg;
            IF @WhatIf = 0 EXEC sp_executesql @sql;

            -- Tag ownership
            IF @WhatIf = 0
            BEGIN
                DECLARE @newObjectId INT = OBJECT_ID(QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName));
                IF @newObjectId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM sys.extended_properties WHERE major_id = @newObjectId AND minor_id = 0 AND name = 'SchemaSmith_Product')
                    EXEC sp_addextendedproperty 'SchemaSmith_Product', @ProductName, 'SCHEMA', @schemaName, 'VIEW', @viewName;
            END;
        END;

        -- Create/update indexes
        IF @needsRecreate = 1 OR @needsIndexUpdate = 1
        BEGIN
            DECLARE @idxName NVARCHAR(200), @idxUnique BIT, @idxClustered BIT,
                    @idxColumns NVARCHAR(MAX), @idxInclude NVARCHAR(MAX),
                    @idxCompression NVARCHAR(200), @idxFillFactor INT;

            -- For index updates on existing views, diff individual indexes
            IF @needsIndexUpdate = 1 AND @existingObjectId IS NOT NULL
            BEGIN
                -- Build existing index metadata from sys catalog
                DECLARE @ExistingIdx TABLE (
                    Name NVARCHAR(200),
                    IsUnique BIT,
                    IsClustered BIT,
                    IndexColumns NVARCHAR(MAX),
                    IncludeColumns NVARCHAR(MAX),
                    CompressionType NVARCHAR(200),
                    [FillFactor] INT
                );

                DELETE FROM @ExistingIdx;
                INSERT INTO @ExistingIdx (Name, IsUnique, IsClustered, IndexColumns, IncludeColumns, CompressionType, [FillFactor])
                SELECT
                    i.name,
                    i.is_unique,
                    CAST(CASE WHEN i.type = 1 THEN 1 ELSE 0 END AS BIT),
                    STRING_AGG(
                        CASE WHEN ic.is_included_column = 0
                            THEN QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
                            ELSE NULL END, ','
                    ) WITHIN GROUP (ORDER BY ic.key_ordinal),
                    (SELECT STRING_AGG(QUOTENAME(c2.name), ',') WITHIN GROUP (ORDER BY ic2.index_column_id)
                       FROM sys.index_columns ic2
                      INNER JOIN sys.columns c2 ON ic2.object_id = c2.object_id AND ic2.column_id = c2.column_id
                      WHERE ic2.object_id = i.object_id AND ic2.index_id = i.index_id AND ic2.is_included_column = 1),
                    ISNULL(p.data_compression_desc, 'NONE'),
                    i.fill_factor
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                LEFT JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id AND p.partition_number = 1
                WHERE i.object_id = @existingObjectId AND i.type > 0
                GROUP BY i.name, i.index_id, i.is_unique, i.type, i.fill_factor, p.data_compression_desc, i.object_id;

                -- Parse desired indexes from JSON
                DECLARE @DesiredIdx TABLE (
                    Name NVARCHAR(200),
                    IsUnique BIT,
                    IsClustered BIT,
                    IndexColumns NVARCHAR(MAX),
                    IncludeColumns NVARCHAR(MAX),
                    CompressionType NVARCHAR(200),
                    [FillFactor] INT
                );

                DELETE FROM @DesiredIdx;
                INSERT INTO @DesiredIdx (Name, IsUnique, IsClustered, IndexColumns, IncludeColumns, CompressionType, [FillFactor])
                SELECT
                    [SchemaSmith].[fn_StripBracketWrapping](JSON_VALUE(idx.value, '$.Name')),
                    CAST(ISNULL(JSON_VALUE(idx.value, '$.Unique'), 'false') AS BIT),
                    CAST(ISNULL(JSON_VALUE(idx.value, '$.Clustered'), 'false') AS BIT),
                    REPLACE(JSON_VALUE(idx.value, '$.IndexColumns'), ', ', ','),
                    REPLACE(ISNULL(JSON_VALUE(idx.value, '$.IncludeColumns'), ''), ', ', ','),
                    ISNULL(JSON_VALUE(idx.value, '$.CompressionType'), 'NONE'),
                    CAST(ISNULL(JSON_VALUE(idx.value, '$.FillFactor'), '0') AS INT)
                FROM OPENJSON(@indexJson) idx;

                -- Determine if the clustered index needs changing
                -- If so, ALL nonclustered indexes must be dropped first (SQL Server requirement)
                DECLARE @clusteredNeedsChange BIT = 0;

                -- Clustered changed or removed
                IF EXISTS (
                    SELECT 1 FROM @ExistingIdx e
                    WHERE e.IsClustered = 1
                    AND (NOT EXISTS (SELECT 1 FROM @DesiredIdx d WHERE d.Name = e.Name AND d.IsClustered = 1)
                         OR EXISTS (SELECT 1 FROM @DesiredIdx d WHERE d.Name = e.Name AND d.IsClustered = 1
                                    AND (d.IsUnique != e.IsUnique OR d.IndexColumns != e.IndexColumns
                                         OR ISNULL(d.IncludeColumns, '') != ISNULL(e.IncludeColumns, '')
                                         OR d.CompressionType != e.CompressionType
                                         OR (@UpdateFillFactor = 1 AND d.[FillFactor] != e.[FillFactor]))))
                )
                    SET @clusteredNeedsChange = 1;

                -- New clustered index where none existed
                IF NOT EXISTS (SELECT 1 FROM @ExistingIdx WHERE IsClustered = 1)
                   AND EXISTS (SELECT 1 FROM @DesiredIdx WHERE IsClustered = 1)
                    SET @clusteredNeedsChange = 1;

                IF @clusteredNeedsChange = 1
                BEGIN
                    -- Clustered index changing — must drop all indexes (nonclustered then clustered)
                    SET @msg = N'Clustered index changed on ' + @schemaName + N'.' + @viewName + N' — dropping all indexes for recreation';
                    EXEC [SchemaSmith].[PrintWithNoWait] @msg;

                    SET @ncDropSql = NULL;
                    SELECT @ncDropSql = STRING_AGG('DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName), '; ')
                    FROM sys.indexes i WHERE i.object_id = @existingObjectId AND i.type > 1;
                    IF @ncDropSql IS NOT NULL AND @WhatIf = 0 EXEC sp_executesql @ncDropSql;

                    SET @clDropSql = NULL;
                    SELECT @clDropSql = 'DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName)
                    FROM sys.indexes i WHERE i.object_id = @existingObjectId AND i.type = 1;
                    IF @clDropSql IS NOT NULL AND @WhatIf = 0 EXEC sp_executesql @clDropSql;
                END
                ELSE
                BEGIN
                    -- Clustered index unchanged — drop only changed and removed nonclustered indexes
                    DECLARE @dropSql NVARCHAR(MAX);

                    -- Drop changed nonclustered indexes (properties differ from spec)
                    SET @dropSql = NULL;
                    SELECT @dropSql = STRING_AGG(
                        'DROP INDEX ' + QUOTENAME(e.Name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName), '; ')
                    FROM @ExistingIdx e
                    INNER JOIN @DesiredIdx d ON d.Name = e.Name
                    WHERE e.IsClustered = 0
                    AND (d.IsUnique != e.IsUnique OR d.IsClustered != e.IsClustered
                         OR d.IndexColumns != e.IndexColumns
                         OR ISNULL(d.IncludeColumns, '') != ISNULL(e.IncludeColumns, '')
                         OR d.CompressionType != e.CompressionType
                         OR (@UpdateFillFactor = 1 AND d.[FillFactor] != e.[FillFactor]));
                    IF @dropSql IS NOT NULL
                    BEGIN
                        SET @msg = N'Dropping changed nonclustered indexes on ' + @schemaName + N'.' + @viewName;
                        EXEC [SchemaSmith].[PrintWithNoWait] @msg;
                        IF @WhatIf = 0 EXEC sp_executesql @dropSql;
                    END;

                    -- Drop removed nonclustered indexes (in DB but not in spec)
                    SET @dropSql = NULL;
                    SELECT @dropSql = STRING_AGG(
                        'DROP INDEX ' + QUOTENAME(e.Name) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName), '; ')
                    FROM @ExistingIdx e
                    WHERE e.IsClustered = 0
                    AND NOT EXISTS (SELECT 1 FROM @DesiredIdx d WHERE d.Name = e.Name);
                    IF @dropSql IS NOT NULL
                    BEGIN
                        SET @msg = N'Dropping removed nonclustered indexes on ' + @schemaName + N'.' + @viewName;
                        EXEC [SchemaSmith].[PrintWithNoWait] @msg;
                        IF @WhatIf = 0 EXEC sp_executesql @dropSql;
                    END;
                END;
            END;

            -- Create missing indexes (clustered first, then nonclustered)
            -- For new views: all indexes are missing
            -- For index updates: only dropped/new indexes are missing (unchanged indexes still exist and are skipped)
            DECLARE idx_cursor CURSOR LOCAL FAST_FORWARD FOR
                SELECT
                    [SchemaSmith].[fn_StripBracketWrapping](JSON_VALUE(idx.value, '$.Name')),
                    CAST(ISNULL(JSON_VALUE(idx.value, '$.Unique'), 'false') AS BIT),
                    CAST(ISNULL(JSON_VALUE(idx.value, '$.Clustered'), 'false') AS BIT),
                    JSON_VALUE(idx.value, '$.IndexColumns'),
                    JSON_VALUE(idx.value, '$.IncludeColumns'),
                    JSON_VALUE(idx.value, '$.CompressionType'),
                    CAST(ISNULL(JSON_VALUE(idx.value, '$.FillFactor'), '0') AS INT)
                FROM OPENJSON(@indexJson) idx
                ORDER BY CASE WHEN ISNULL(JSON_VALUE(idx.value, '$.Clustered'), 'false') = 'true' THEN 0 ELSE 1 END;

            OPEN idx_cursor;
            FETCH NEXT FROM idx_cursor INTO @idxName, @idxUnique, @idxClustered, @idxColumns, @idxInclude, @idxCompression, @idxFillFactor;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                -- Skip indexes that already exist (unchanged during index-only update)
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes si
                    WHERE si.object_id = OBJECT_ID(QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName))
                    AND si.name = @idxName
                )
                BEGIN
                    SET @sql = 'CREATE ';
                    IF @idxUnique = 1 SET @sql += 'UNIQUE ';
                    IF @idxClustered = 1 SET @sql += 'CLUSTERED ';
                    SET @sql += 'INDEX ' + QUOTENAME(@idxName) + ' ON ' + QUOTENAME(@schemaName) + '.' + QUOTENAME(@viewName) + ' (' + @idxColumns + ')';
                    IF @idxInclude IS NOT NULL SET @sql += ' INCLUDE (' + @idxInclude + ')';

                    DECLARE @withOpts NVARCHAR(MAX) = '';
                    IF @idxCompression IS NOT NULL AND @idxCompression != 'NONE'
                        SET @withOpts += 'DATA_COMPRESSION = ' + @idxCompression;
                    IF @idxFillFactor > 0 OR (@UpdateFillFactor = 1 AND @idxFillFactor > 0)
                    BEGIN
                        IF LEN(@withOpts) > 0 SET @withOpts += ', ';
                        SET @withOpts += 'FILLFACTOR = ' + CAST(@idxFillFactor AS NVARCHAR(10));
                    END;
                    IF LEN(@withOpts) > 0 SET @sql += ' WITH (' + @withOpts + ')';

                    EXEC [SchemaSmith].[PrintWithNoWait] @sql;
                    IF @WhatIf = 0 EXEC sp_executesql @sql;
                END;

                FETCH NEXT FROM idx_cursor INTO @idxName, @idxUnique, @idxClustered, @idxColumns, @idxInclude, @idxCompression, @idxFillFactor;
            END;
            CLOSE idx_cursor;
            DEALLOCATE idx_cursor;
        END;

        FETCH NEXT FROM view_cursor INTO @schemaName, @viewName, @defn, @indexJson;
    END;
    CLOSE view_cursor;
    DEALLOCATE view_cursor;
END;
