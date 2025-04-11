CREATE OR ALTER PROCEDURE [SchemaSmith].[TableQuench] 
  @ProductName VARCHAR(50),
  @TableDefinitions VARCHAR(MAX),
  @WhatIf BIT = 0,
  @DropUnknownIndexes BIT = 0,
  @DropTablesRemovedFromProduct BIT = 1,
  @UpdateFillFactor BIT = 1
AS
BEGIN TRY
  DECLARE @v_SQL VARCHAR(MAX) = ''
  SET NOCOUNT ON
  RAISERROR('Parse Tables from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #TableDefinitions
  SELECT [Schema] = SchemaSmith.fn_SafeBracketWrap(ISNULL([Schema], 'dbo')), [Name] = SchemaSmith.fn_SafeBracketWrap([Name]), [CompressionType] = ISNULL([CompressionType], 'NONE'), [IsTemporal] = ISNULL([IsTemporal], 0),
         [Indexes], [Columns], [Statistics], [FullTextIndex], [ForeignKeys], [CheckConstraints]
    INTO #TableDefinitions
    FROM OPENJSON(@TableDefinitions) WITH (
      [Schema] VARCHAR(500) '$.Schema',
      [Name] VARCHAR(500) '$.Name',
      [CompressionType] VARCHAR(100) '$.CompressionType',
      [IsTemporal] BIT '$.IsTemporal',
	  [Indexes] NVARCHAR(MAX) '$.Indexes' AS JSON,
      [Columns] NVARCHAR(MAX) '$.Columns' AS JSON,
	  [Statistics] NVARCHAR(MAX) '$.Statistics' AS JSON,
	  [FullTextIndex] NVARCHAR(MAX) '$.FullTextIndex' AS JSON,
      [ForeignKeys] NVARCHAR(MAX) '$.ForeignKeys' AS JSON,
      [CheckConstraints] NVARCHAR(MAX) '$.CheckConstraints' AS JSON
      ) t;

  DROP TABLE IF EXISTS #Tables
  SELECT [Schema], [Name], [CompressionType], [IsTemporal],
         CONVERT(BIT, CASE WHEN OBJECT_ID([Schema] + '.' + [Name], 'U') IS NULL THEN 1 ELSE 0 END) AS NewTable
    INTO #Tables
    FROM #TableDefinitions WITH (NOLOCK)
  
  RAISERROR('Parse Indexes from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #Indexes
  SELECT t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), [CompressionType] = ISNULL(i.[CompressionType], 'NONE'), [PrimaryKey] = ISNULL(i.[PrimaryKey], 0), 
         [Unique] = COALESCE(NULLIF(i.[Unique], 0), NULLIF(i.[PrimaryKey], 0), i.[UniqueConstraint], 0),
         [UniqueConstraint] = ISNULL(i.[UniqueConstraint], 0), [Clustered] = ISNULL(i.[Clustered], 0), [ColumnStore] = ISNULL(i.[ColumnStore], 0), [FillFactor] = ISNULL(NULLIF(i.[FillFactor], 0), 100),
         i.[FilterExpression], 
         [IndexColumns] = (SELECT STRING_AGG(CASE WHEN RTRIM([value]) LIKE '% DESC' 
                                                  THEN SchemaSmith.fn_SafeBracketWrap(SUBSTRING(RTRIM([value]), 1, LEN(RTRIM([value])) - 5)) + ' DESC'
                                                  ELSE SchemaSmith.fn_SafeBracketWrap([value])
                                                  END, ',') 
                             FROM STRING_SPLIT(i.[IndexColumns], ',') 
                             WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [IncludeColumns] = (SELECT STRING_AGG(SchemaSmith.fn_SafeBracketWrap([value]), ',') WITHIN GROUP (ORDER BY SchemaSmith.fn_SafeBracketWrap([value]))
                               FROM STRING_SPLIT(i.[IncludeColumns], ',') 
                               WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '')
    INTO #Indexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(Indexes) WITH (
      [IndexName] VARCHAR(500) '$.Name',
      [CompressionType] VARCHAR(100) '$.CompressionType',
      [PrimaryKey] BIT '$.PrimaryKey',
      [Unique] BIT '$.Unique',
	  [UniqueConstraint] BIT '$.UniqueConstraint',
      [Clustered] BIT '$.Clustered',
      [ColumnStore] BIT '$.ColumnStore',
      [FillFactor] TINYINT '$.FillFactor',
      [FilterExpression] VARCHAR(MAX) '$.FilterExpression',
      [IndexColumns] VARCHAR(MAX) '$.IndexColumns',
      [IncludeColumns] VARCHAR(MAX) '$.IncludeColumns'
      ) i;
  
  RAISERROR('Parse Columns from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #Columns
  SELECT t.[Schema], t.[Name] AS [TableName], [ColumnName] = SchemaSmith.fn_SafeBracketWrap(c.[ColumnName]), c.[DataType], [Nullable] = ISNULL(c.[Nullable], 0), 
         c.[Default], c.[CheckExpression], c.[ComputedExpression], [Persisted] = ISNULL(c.[Persisted], 0),
         CONVERT(BIT, CASE WHEN NOT EXISTS (SELECT * FROM #Tables x WHERE x.[Name] = t.[Name] AND x.[Schema] = t.[Schema] AND x.NewTable = 1)
                            AND COLUMNPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name], 'U'), SchemaSmith.fn_StripBracketWrapping([ColumnName]), 'ColumnId') IS NULL
                           THEN 1 ELSE 0 END) AS NewColumn,
         SchemaSmith.fn_SafeBracketWrap(c.[ColumnName]) + ' ' +
         -- For computed columns only the expression is needed
         CASE WHEN RTRIM(ISNULL([ComputedExpression], '')) <> '' THEN 'AS (' + ComputedExpression + ')' + CASE WHEN ISNULL(c.[Persisted], 0) = 1 THEN ' PERSISTED' ELSE '' END
              -- Otherwise build the column definition
              ELSE UPPER([DataType]) + CASE WHEN ISNULL(Nullable, 0) = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                   CASE WHEN RTRIM(ISNULL([Default], '')) <> '' THEN ' DEFAULT ' + [Default] ELSE '' END
              END AS [ColumnScript]
    INTO #Columns
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(Columns) WITH (
      [ColumnName] VARCHAR(500) '$.Name',
      [DataType] VARCHAR(100) '$.DataType',
      [Nullable] BIT '$.Nullable',
      [Default] VARCHAR(MAX) '$.Default',
      [CheckExpression] VARCHAR(MAX) '$.CheckExpression',
      [ComputedExpression] VARCHAR(MAX) '$.ComputedExpression',
      [Persisted] BIT '$.Persisted'
      ) c;
  
  RAISERROR('Parse Foreign Keys from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ForeignKeys
  SELECT t.[Schema], t.[Name] AS [TableName], [KeyName] = SchemaSmith.fn_SafeBracketWrap(f.[KeyName]), 
         [RelatedTableSchema] = SchemaSmith.fn_SafeBracketWrap(ISNULL(f.[RelatedTableSchema], 'dbo')), [RelatedTable] = SchemaSmith.fn_SafeBracketWrap(f.[RelatedTable]), 
         [Columns] = (SELECT STRING_AGG(SchemaSmith.fn_SafeBracketWrap([value]), ',') FROM STRING_SPLIT(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [RelatedColumns] = (SELECT STRING_AGG(SchemaSmith.fn_SafeBracketWrap([value]), ',') FROM STRING_SPLIT(f.[RelatedColumns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [CascadeOnDelete] = ISNULL(f.[CascadeOnDelete], 0), [CascadeOnUpdate] = ISNULL(f.[CascadeOnUpdate], 0)
    INTO #ForeignKeys
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(ForeignKeys) WITH (
      [KeyName] VARCHAR(500) '$.Name',
      [Columns] VARCHAR(MAX) '$.Columns',
      [RelatedTableSchema] VARCHAR(500) '$.RelatedTableSchema',
      [RelatedTable] VARCHAR(500) '$.RelatedTable',
      [RelatedColumns] VARCHAR(MAX) '$.RelatedColumns',
      [CascadeOnDelete] BIT '$.CascadeOnDelete',
      [CascadeOnUpdate] BIT '$.CascadeOnUpdate'
      ) f;
  
  RAISERROR('Parse Table Level Check Constraints from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #CheckConstraints
  SELECT t.[Schema], t.[Name] AS [TableName], c.[ConstraintName], c.[Expression]
    INTO #CheckConstraints
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(CheckConstraints) WITH (
      [ConstraintName] VARCHAR(500) '$.Name',
      [Expression] VARCHAR(MAX) '$.Expression'
      ) c;
  
  RAISERROR('Parse Statistics from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #Statistics
  SELECT t.[Schema], t.[Name] AS [TableName], [StatisticName] = SchemaSmith.fn_SafeBracketWrap(s.[StatisticName]), [SampleSize] = ISNULL(s.[SampleSize], 0), s.[FilterExpression],
         [Columns] = (SELECT STRING_AGG(SchemaSmith.fn_SafeBracketWrap([value]), ',') FROM STRING_SPLIT(s.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '')
    INTO #Statistics
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON([Statistics]) WITH (
      [StatisticName] VARCHAR(500) '$.Name',
      [SampleSize] TINYINT '$.SampleSize',
      [FilterExpression] VARCHAR(MAX) '$.FilterExpression',
      [Columns] VARCHAR(MAX) '$.Columns'
      ) s;
  
  RAISERROR('Parse Full Text Indexes from Json', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #FullTextIndexes
  SELECT t.[Schema], t.[Name] AS [TableName], [FullTextCatalog] = SchemaSmith.fn_SafeBracketWrap(f.[FullTextCatalog]), [KeyIndex] = SchemaSmith.fn_SafeBracketWrap(f.[KeyIndex]), 
         f.[ChangeTracking], [StopList] = SchemaSmith.fn_SafeBracketWrap(COALESCE(NULLIF(RTRIM(f.[StopList]), ''), 'SYSTEM')),
         [Columns] = (SELECT STRING_AGG(SchemaSmith.fn_SafeBracketWrap([value]), ',') FROM STRING_SPLIT(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '')
    INTO #FullTextIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON([FullTextIndex]) WITH (
      [Columns] VARCHAR(MAX) '$.Columns',
      [FullTextCatalog] VARCHAR(500) '$.FullTextCatalog',
      [KeyIndex] VARCHAR(500) '$.KeyIndex',
      [ChangeTracking] VARCHAR(500) '$.ChangeTracking',
      [StopList] VARCHAR(500) '$.StopList'
      ) f;
  
  -- Clustered index compression overrides the table compression
  RAISERROR('Override table compression to match clustered index', 10, 1) WITH NOWAIT
  UPDATE t
    SET [CompressionType] = i.[CompressionType]
    FROM #Tables t
    JOIN #Indexes i WITH (NOLOCK) ON i.[Schema] = t.[Schema]
                                 AND i.[TableName] = t.[Name]
                                 AND i.[Clustered] = 1
 
  RAISERROR('Get Schema List', 10, 1) WITH NOWAIT
  SELECT DISTINCT t.[Schema]
    INTO #SchemaList
    FROM #Tables t WITH (NOLOCK)

  RAISERROR('Turn off Temporal Tracking for tables no longer defined temporal', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Turn OFF Temporal Tracking for ' + T.[Schema] + '.' + T.[Name] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' SET (SYSTEM_VERSIONING = OFF);' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' DROP PERIOD FOR SYSTEM_TIME;', CHAR(13) + CHAR(10))
    FROM #Tables T WITH (NOLOCK)
    WHERE t.IsTemporal = 0
      AND OBJECTPROPERTY(OBJECT_ID([Schema] + '.' + [Name]), 'TableTemporalType') = 2
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect table level extended properties', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #TableProperties
  SELECT [Schema], objname COLLATE DATABASE_DEFAULT AS TableName, x.[Name] COLLATE DATABASE_DEFAULT AS PropertyName, CONVERT(VARCHAR(50), x.[value]) COLLATE DATABASE_DEFAULT AS [value]
    INTO #TableProperties
    FROM #SchemaList WITH (NOLOCK)
    CROSS APPLY fn_listextendedproperty(default, 'Schema', SchemaSmith.fn_StripBracketWrapping([Schema]), 'Table', default, default, default) x
    WHERE x.[Name] COLLATE DATABASE_DEFAULT = 'ProductName'
  
  RAISERROR('Validate Table Ownership', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Table ' + tp.[Schema] + '.' + tp.[TableName] + ' owned by different product. [' + tp.[Value] + ']'', 10, 1) WITH NOWAIT;', CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    JOIN #TableProperties tp WITH (NOLOCK) ON t.[Schema] = tp.[Schema]
                                          AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName
    WHERE tp.[value] <> @ProductName
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  IF EXISTS (SELECT *
               FROM #Tables t WITH (NOLOCK)
               JOIN #TableProperties tp WITH (NOLOCK) ON t.[Schema] = tp.[Schema]
                                                     AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName
               WHERE tp.[value] <> @ProductName)
  BEGIN
    RAISERROR('One or more tables in this quench are already owned by another product', 16, 1) WITH NOWAIT
  END
  
  IF @DropTablesRemovedFromProduct = 1
  BEGIN
    RAISERROR('Identify tables removed from the product', 10, 1) WITH NOWAIT
    DROP TABLE IF EXISTS #TablesRemovedFromProduct
    SELECT tp.[Schema], tp.TableName
      INTO #TablesRemovedFromProduct
      FROM #TableProperties tp WITH (NOLOCK)
      WHERE tp.[value] = @ProductName
        AND NOT EXISTS (SELECT * 
                          FROM #Tables t WITH (NOLOCK) 
                          WHERE t.[Schema] = tp.[Schema] 
                            AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName)

    IF EXISTS (SELECT * FROM #TablesRemovedFromProduct WITH (NOLOCK))
    BEGIN
      RAISERROR('Drop tables removed from the product', 10, 1) WITH NOWAIT
      SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping table ' + t.[Schema] + '.' + t.[TableName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                 'DROP TABLE IF EXISTS ' + t.[Schema] + '.[' + t.[TableName] + '];', CHAR(13) + CHAR(10))
        FROM #TablesRemovedFromProduct t WITH (NOLOCK)
      IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
    END
  END
  
  RAISERROR('Collect index level extended properties', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexProperties
  SELECT t.[Schema], t.[Name] AS TableName, objname COLLATE DATABASE_DEFAULT AS IndexName, x.[Name] COLLATE DATABASE_DEFAULT AS PropertyName, CONVERT(VARCHAR(50), x.[value]) COLLATE DATABASE_DEFAULT AS [value]
    INTO #IndexProperties
    FROM #Tables t WITH (NOLOCK)
    CROSS APPLY fn_listextendedproperty(default, 'Schema', SchemaSmith.fn_StripBracketWrapping(t.[Schema]), 'Table', SchemaSmith.fn_StripBracketWrapping(t.[Name]), 'Index', default) x
    WHERE x.[Name] COLLATE DATABASE_DEFAULT = 'ProductName'
  
  RAISERROR('Identify indexes removed from the product', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesRemovedFromProduct
  SELECT xp.[Schema], xp.TableName, xp.IndexName, IsConstraint = CAST(CASE WHEN OBJECT_ID(xp.[Schema] + '.' + xp.IndexName) IS NOT NULL THEN 1 ELSE 0 END AS BIT)
    INTO #IndexesRemovedFromProduct
    FROM #IndexProperties xp WITH (NOLOCK)
    WHERE xp.[value] = @ProductName
      AND NOT EXISTS (SELECT * 
                        FROM #Indexes i WITH (NOLOCK) 
                        WHERE i.[Schema] = xp.[Schema] 
                          AND i.TableName = xp.TableName
                          AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = xp.IndexName)

  RAISERROR('Detect Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ColumnChanges
  SELECT c.[Schema], c.[TableName], c.[ColumnName],
         -- For computed columns, only the expression is needed
         CASE WHEN RTRIM(ISNULL([ComputedExpression], '')) <> '' 
              THEN 'AS (' + ComputedExpression + ')' + CASE WHEN c.[Persisted] = 1 THEN ' PERSISTED' ELSE '' END
              -- Otherwise we need to build the column definition
              ELSE UPPER(LEFT([DataType], COALESCE(NULLIF(CHARINDEX('IDENTITY', [DataType]), 0), LEN([DataType]) + 1) - 1)) + CASE WHEN Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END
              END AS [ColumnScript],
         CAST(CASE WHEN cc.[definition] IS NOT NULL OR RTRIM(ISNULL([ComputedExpression], '')) <> ''
                     OR (ident.column_id IS NULL AND [DataType] LIKE '%IDENTITY%') -- switching to identity... requires drop and recreate column
                   THEN 1 ELSE 0 END AS BIT) AS MustDropAndRecreate,
         CAST(0 AS BIT) AS DropOnly
    INTO #ColumnChanges
    FROM #Tables T WITH (NOLOCK)
    JOIN #Columns c WITH (NOLOCK) ON C.[Schema] = T.[Schema] 
                                 AND C.[TableName] = T.[Name]
                                 AND C.[NewColumn] = 0
    JOIN INFORMATION_SCHEMA.COLUMNS ic WITH (NOLOCK) ON ic.TABLE_SCHEMA = SchemaSmith.fn_StripBracketWrapping(C.[Schema])
                                                    AND ic.TABLE_NAME = SchemaSmith.fn_StripBracketWrapping(C.[TableName])
                                                    AND ic.COLUMN_NAME = SchemaSmith.fn_StripBracketWrapping(C.[ColumnName])
    JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(ic.TABLE_SCHEMA + '.' + ic.TABLE_NAME) AND sc.[name] = ic.COLUMN_NAME
    JOIN (SELECT CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
                      THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE, st.user_type_id
            FROM sys.types st WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
    LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                      AND ident.[object_id] = OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME)
    LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = SchemaSmith.fn_StripBracketWrapping(c.ColumnName)
                                                   AND cc.[object_id] = OBJECT_ID(C.[Schema] + '.' + C.[TableName])
    WHERE t.NewTable = 0
      AND (UPPER(USER_TYPE) + CASE WHEN USER_TYPE LIKE '%CHAR' OR USER_TYPE LIKE '%BINARY'
                                   THEN '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CONVERT(VARCHAR(20), CHARACTER_MAXIMUM_LENGTH) END + ')'
                                   WHEN USER_TYPE IN ('NUMERIC', 'DECIMAL')
                                   THEN  '(' + CONVERT(VARCHAR(20), NUMERIC_PRECISION) + ', ' + CONVERT(VARCHAR(20), NUMERIC_SCALE) + ')'
                                   WHEN USER_TYPE = 'DATETIME2'
                                   THEN  '(' + CONVERT(VARCHAR(20), DATETIME_PRECISION) + ')'
                                   ELSE '' END +
                              CASE WHEN ident.column_id IS NOT NULL
                                   THEN ' IDENTITY(' + CONVERT(VARCHAR(20), ident.seed_value) + ', ' + CONVERT(VARCHAR(20), ident.increment_value) + ')'
                                   ELSE '' END  <> c.DataType
        OR CASE WHEN c.Nullable = 1 THEN 'YES' ELSE 'NO' END <> ic.IS_NULLABLE
        OR ISNULL(SchemaSmith.fn_StripParenWrapping(cc.[definition]), '') <> ISNULL(c.ComputedExpression, '')
        OR ISNULL(cc.is_persisted, 0) <> ISNULL(c.[Persisted], 0))
  
  RAISERROR('Detect Computed Columns Impacted by Other Column Changes', 10, 1) WITH NOWAIT
  INSERT #ColumnChanges ([Schema], [TableName], [ColumnName], [ColumnScript], MustDropAndRecreate, [DropOnly])
    SELECT C.[Schema], C.[TableName], c.[ColumnName], 
           [ColumnScript] = 'AS (' + ComputedExpression + ')' + CASE WHEN c.[Persisted] = 1 THEN ' PERSISTED' ELSE '' END, 
           MustDropAndRecreate = CAST(1 AS BIT), [DropOnly] = CAST(0 AS BIT)
      FROM #ColumnChanges cc WITH (NOLOCK)
      JOIN sys.computed_columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName])
                                                AND sc.[definition] LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'
      JOIN #Columns c WITH (NOLOCK) ON C.[Schema] = cc.[Schema] 
                                   AND C.[TableName] = cc.[TableName]
                                   AND c.[ColumnName] = cc.[ColumnName]
      WHERE NOT EXISTS (SELECT * FROM #ColumnChanges cc2 WITH (NOLOCK) WHERE cc2.[Schema] = cc.[Schema] AND cc2.[TableName] = cc.[TableName] AND cc2.[ColumnName] = cc.[ColumnName])
  
  RAISERROR('Detect Column Drops', 10, 1) WITH NOWAIT
  INSERT #ColumnChanges ([Schema], [TableName], [ColumnName], [ColumnScript], MustDropAndRecreate, [DropOnly])
    SELECT t.[Schema], [TableName] = t.[Name], [ColumnName] = '[' + COLUMN_NAME + ']', '', 0, 1
      FROM #Tables t WITH (NOLOCK)
      JOIN INFORMATION_SCHEMA.COLUMNS WITH (NOLOCK) ON TABLE_SCHEMA = SchemaSmith.fn_StripBracketWrapping(t.[Schema])
                                                   AND TABLE_NAME = SchemaSmith.fn_StripBracketWrapping(t.[Name]) 
      WHERE NOT EXISTS (SELECT * 
                          FROM #Columns c WITH (NOLOCK)
                          WHERE c.[Schema] = t.[Schema]
                            AND c.[TableName] = t.[Name]
                            AND SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]) = COLUMN_NAME)
        AND NOT (t.IsTemporal = 1 AND COLUMN_NAME IN ('ValidFrom', 'ValidTo'))
  
  RAISERROR('Collect Foreign Keys To Drop', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #FKsToDrop
  SELECT t.[Schema], [TableName] = t.[Name], [FKName] = fk.[Name]
    INTO #FKsToDrop
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.parent_object_id = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    WHERE NOT EXISTS (SELECT * FROM #ForeignKeys fk2 WITH (NOLOCK) WHERE t.[Schema] = fk2.[Schema] AND t.[Name] = fk2.[TableName] AND fk.[name] = SchemaSmith.fn_StripBracketWrapping(fk2.[KeyName]))

  RAISERROR('Drop Foreign Keys No Longer Defined In The Product', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping foreign Key ' + df.[Schema] + '.' + df.[TableName] + '.' + df.[FKName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + df.[Schema] + '.' + df.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + df.[FKName] + ';', CHAR(13) + CHAR(10))
    FROM #FKsToDrop df WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Fulltext Indexes To Drop Based On Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #FTIndexesToDropForChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName]
    INTO #FTIndexesToDropForChanges
    FROM sys.fulltext_index_columns ic WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON ic.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                                        AND COL_NAME(ic.[object_id], ic.column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
  
  RAISERROR('Drop FullText Indexes Referencing Modified Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping fulltext index on ' + di.[Schema] + '.' + di.[TableName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'DROP FULLTEXT INDEX ON ' + di.[Schema] + '.' + di.[TableName] + ';', CHAR(13) + CHAR(10))
    FROM #FTIndexesToDropForChanges di WITH (NOLOCK)
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(di.[Schema] + '.' + di.[TableName])
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing FullText Indexes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingFullTextIndexes
  SELECT t.[Schema], [TableName] = t.[Name],
         (SELECT STRING_AGG('[' + COL_NAME(fc.[object_id], fc.column_id) + ']', ',') WITHIN GROUP (ORDER BY COL_NAME(fc.[object_id], fc.column_id))
            FROM sys.fulltext_index_columns fc WITH (NOLOCK)
            WHERE fi.[object_id] = fc.[object_id]) AS [Columns],
         FullTextCatalog = '[' + (SELECT c.[name] COLLATE DATABASE_DEFAULT FROM sys.fulltext_catalogs c WITH (NOLOCK) WHERE c.fulltext_catalog_id = fi.fulltext_catalog_id) + ']',
         KeyIndex = '[' + (SELECT i.[Name] COLLATE DATABASE_DEFAULT FROM sys.indexes i WITH (NOLOCK) WHERE i.[object_id] = fi.[object_id] AND i.[index_id] = fi.[unique_index_id]) + ']',
         ChangeTracking = change_tracking_state_desc COLLATE DATABASE_DEFAULT,
         [StopList] = '[' + COALESCE((SELECT fs.[name] COLLATE DATABASE_DEFAULT FROM sys.fulltext_stoplists fs WITH (NOLOCK) WHERE fs.stoplist_id = fi.stoplist_id), 'SYSTEM') + ']'
    INTO #ExistingFullTextIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    WHERE t.NewTable = 0
  
  RAISERROR('Identify Indexes To Drop Based On Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesToDropForColumnChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName], IndexName = i.[name],
         IsConstraint = CAST(CASE WHEN i.is_primary_key = 1 OR i.is_unique_constraint = 1 THEN 1 ELSE 0 END AS BIT),
         IsUnique = i.is_unique
    INTO #IndexesToDropForColumnChanges
    FROM sys.indexes i WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON i.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
    LEFT JOIN sys.index_columns ic WITH (NOLOCK) ON ic.[object_id] = i.[object_id]
                                                AND ic.[index_id] = i.[index_id]
                                                AND COL_NAME(ic.[object_id], ic.column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
    WHERE ic.column_id IS NOT NULL
       OR i.filter_definition LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'
  
  -- Handle table compression changes
  RAISERROR('Fixup Table Compression', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Altering table compression for ' + t.[Schema] + '.' + t.[Name] + ' TO ' + t.[CompressionType] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + t.[Schema] + '.' + t.[Name] + ' REBUILD PARTITION=ALL WITH (DATA_COMPRESSION=' + t.[CompressionType] + ');', CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    LEFT JOIN sys.partitions p WITH (NOLOCK) ON p.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                            AND p.index_id < 2
    WHERE t.NewTable = 0
      AND COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') <> t.[CompressionType]
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  -- Handle index compression changes
  RAISERROR('Fixup Index Compression', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Altering index compression for ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ' TO ' + i.[CompressionType] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' REBUILD PARTITION=ALL WITH (DATA_COMPRESSION=' + i.[CompressionType] + ');', CHAR(13) + CHAR(10))
    FROM #Indexes i WITH (NOLOCK) 
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName])
                                     AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    LEFT JOIN sys.partitions p WITH (NOLOCK) ON p.[object_id] = si.[object_id]
                                            AND p.index_id = si.index_id
    WHERE COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') <> i.[CompressionType]
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect Existing Index Definitions', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingIndexes
  SELECT xSchema = t.[Schema], [xTableName] = t.[Name], [xIndexName] = CAST(si.[Name] AS VARCHAR(500)),
         IsConstraint = CAST(CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1 THEN 1 ELSE 0 END AS BIT),
         IsUnique = si.is_unique, [FillFactor] = ISNULL(NULLIF(si.fill_factor, 0), 100),
         IndexScript = 'CREATE ' + 
                       CASE WHEN si.is_unique = 1 THEN 'UNIQUE ' ELSE '' END + 
                       CASE WHEN si.[type] IN (1, 5) THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                       'INDEX [' + si.[Name] + '] ON ' + t.[Schema] + '.' + t.[Name] + ' (' +
                       (SELECT STRING_AGG('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END, ',') WITHIN GROUP (ORDER BY key_ordinal)
                          FROM sys.index_columns ic WITH (NOLOCK)
                          WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 0) + ')' +
                       CASE WHEN EXISTS (SELECT * FROM sys.index_columns ic WITH (NOLOCK) WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1)
                            THEN ' INCLUDE (' +
                                 (SELECT STRING_AGG('[' + COL_NAME(ic.[object_id], ic.column_id) + ']', ',') WITHIN GROUP (ORDER BY COL_NAME(ic.[object_id], ic.column_id))
                                    FROM sys.index_columns ic WITH (NOLOCK)
                                    WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) + ')'
                            ELSE '' END +
                       CASE WHEN si.has_filter = 1 THEN ' WHERE ' + SchemaSmith.fn_StripParenWrapping(si.filter_definition) ELSE '' END +
                       CASE WHEN COALESCE(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT IN ('NONE', 'ROW', 'PAGE')
                            THEN ' WITH (DATA_COMPRESSION=' + COALESCE(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT + ')'
                            ELSE '' END
    INTO #ExistingIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                     AND si.index_id > 0
                                     AND is_hypothetical = 0
                                     AND is_disabled = 0
    LEFT JOIN sys.partitions p WITH (NOLOCK) ON p.[object_id] = si.[object_id]
                                            AND p.index_id = si.index_id
    WHERE t.NewTable = 0
    
  RAISERROR('Detect Index Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexChanges
  SELECT i.[Schema], i.[TableName], i.[IndexName], ei.[IsConstraint], IsUnique = i.[Unique]
    INTO #IndexChanges
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE EXISTS (SELECT * 
                    FROM sys.indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND ei.IndexScript <> 'CREATE ' + 
                            CASE WHEN i.[Unique] = 1 THEN 'UNIQUE ' ELSE '' END + 
                            CASE WHEN i.[Clustered] = 1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                            'INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[IndexColumns] + ')' +
                            CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END +
                            CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
                            CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE')
                                 THEN ' WITH (DATA_COMPRESSION=' + RTRIM(ISNULL(i.[CompressionType], '')) + ')'
                                 ELSE '' END

  RAISERROR('Detect Index Renames', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexRenames
  SELECT i.[Schema], i.[TableName], [NewName] = i.[IndexName], ei.[IsConstraint], IsUnique = i.[Unique], [OldName] = ei.[xIndexName]
    INTO #IndexRenames
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] <> SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE EXISTS (SELECT * 
                    FROM sys.indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND REPLACE(ei.IndexScript, ei.[xIndexName], 'IndexName') = 'CREATE ' + 
                                                                  CASE WHEN i.[Unique] = 1 OR i.[PrimaryKey] = 1 THEN 'UNIQUE ' ELSE '' END + 
                                                                  CASE WHEN i.[Clustered] = 1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
	                                                              'INDEX [IndexName] ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[IndexColumns] + ')' +
						                                          CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END +
                                                                  CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
                                                                  CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE')
                                                                       THEN ' WITH (DATA_COMPRESSION=' + RTRIM(ISNULL(i.[CompressionType], '')) + ')'
                                                                       ELSE '' END
  
  RAISERROR('Handle Renamed Indexes And Unique Constraints', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Renaming ' + [OldName] + ' to ' + [NewName] + ' ON ' + ir.[Schema] + '.' + ir.[TableName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             CASE WHEN IsConstraint = 1
                                  THEN CASE WHEN OBJECT_ID(ir.[Schema] + '.' + ir.[NewName]) IS NULL
                                            THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''OBJECT'';'
                                            ELSE 'ALTER TABLE ' + ir.[Schema] + '.' + ir.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + ir.[OldName] + '];'
                                            END
                                  ELSE CASE WHEN INDEXPROPERTY(OBJECT_ID(ir.[Schema] + '.' + ir.[TableName]), SchemaSmith.fn_StripBracketWrapping(ir.[NewName]), 'IndexID') IS NULL
                                            THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(ir.[TableName]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''INDEX'';'
                                            ELSE 'DROP INDEX IF EXISTS ' + ir.[Schema] + '.' + ir.[TableName] + '.[' + ir.[OldName] + '];'
                                            END
                                  END, CHAR(13) + CHAR(10))
    FROM #IndexRenames ir WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Identify unknown and modified indexes to drop', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesToDrop
  SELECT [Schema] = CAST([Schema] AS VARCHAR(500)), [TableName] = CAST([TableName] AS VARCHAR(500)), 
         [IndexName] = CAST(SchemaSmith.fn_StripBracketWrapping([IndexName]) AS VARCHAR(500)), [IsConstraint], [IsUnique] = i.[is_unique]
    INTO #IndexesToDrop
    FROM #IndexesRemovedFromProduct ir WITH (NOLOCK)
    JOIN sys.indexes i WITH (NOLOCK) ON i.[object_id] = OBJECT_ID([Schema] + '.' + [TableName]) AND i.[Name] = SchemaSmith.fn_StripBracketWrapping([IndexName])
  UNION
  SELECT [Schema], [TableName], SchemaSmith.fn_StripBracketWrapping([IndexName]), [IsConstraint], [IsUnique]
    FROM #IndexesToDropForColumnChanges WITH (NOLOCK)
  UNION
  SELECT [xSchema], [xTableName], [xIndexName], [IsConstraint], [IsUnique]
    FROM #ExistingIndexes ei WITH (NOLOCK)
    WHERE @DropUnknownIndexes = 1
      AND NOT EXISTS (SELECT * FROM #Indexes i WITH (NOLOCK) WHERE i.[Schema] = ei.[xSchema] AND i.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i.[IndexName]) = ei.[xIndexName])
  UNION
  SELECT [Schema], [TableName], SchemaSmith.fn_StripBracketWrapping([IndexName]), [IsConstraint], [IsUnique]
    FROM #IndexChanges WITH (NOLOCK)
  
  RAISERROR('Drop Referencing Foreign Keys When Dropping Unique Indexes', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping foreign Key ' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) + '.' + fk.[name] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE [' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + fk.[name] + '];', CHAR(13) + CHAR(10))
    FROM #IndexesToDrop di WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.referenced_object_id = OBJECT_ID(di.[Schema] + '.' + di.[TableName])
    WHERE IsConstraint = 1 OR IsUnique = 1
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop FullText Indexes Referencing Unique Indexes That Will Be Dropped', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping fulltext index on ' + ef.[Schema] + '.' + ef.[TableName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'DROP FULLTEXT INDEX ON ' + ef.[Schema] + '.' + ef.[TableName] + ';', CHAR(13) + CHAR(10))
    FROM #IndexesToDrop id WITH (NOLOCK)
    JOIN #ExistingFullTextIndexes ef WITH (NOLOCK) ON id.[Schema] = ef.[Schema]
                                                  AND id.[TableName] = ef.[TableName]
                                                  AND id.[IndexName] = SchemaSmith.fn_StripBracketWrapping(ef.[KeyIndex])
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(ef.[Schema] + '.' + ef.[TableName])
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Unknown and Modified Indexes', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping ' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + di.[Schema] + '.' + di.[TableName] + '.' + di.[IndexName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             CASE WHEN IsConstraint = 1
                                  THEN 'ALTER TABLE ' + di.[Schema] + '.' + di.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + di.[IndexName] + '];'
                                  ELSE 'DROP INDEX IF EXISTS ' + di.[Schema] + '.' + di.[TableName] + '.[' + di.[IndexName] + '];'
                                  END, CHAR(13) + CHAR(10))
    FROM #IndexesToDrop di WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  IF @UpdateFillFactor = 1
  BEGIN
    RAISERROR('Fixup Modified Fillfactors', 10, 1) WITH NOWAIT
    SELECT @v_SQL = STRING_AGG('RAISERROR(''  Fixup ' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ' fillfactor in ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ''', 10, 1) WITH NOWAIT; ' + 
                               'ALTER INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' REBUILD WITH (FILLFACTOR = ' + CONVERT(VARCHAR(5), i.[FillFactor]) + ', SORT_IN_TEMPDB = ON);', CHAR(13) + CHAR(10))
      FROM #ExistingIndexes ei WITH (NOLOCK)
      JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                   AND ei.[xTableName] = i.[TableName]
                                   AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
      WHERE ei.[FillFactor] <> i.[FillFactor]
        AND INDEXPROPERTY(OBJECT_ID(i.[Schema] + '.' + i.[TableName]), ei.[xIndexName], 'IndexID') IS NOT NULL
    IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  END
  
  RAISERROR('Identify Statistics To Drop Based On Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #StatisticsToDropForChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName], [StatName] = i.[name]
    INTO #StatisticsToDropForChanges
    FROM sys.stats i WITH (NOLOCK) 
    JOIN #ColumnChanges cc WITH (NOLOCK) ON i.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
    LEFT JOIN sys.stats_columns ic WITH (NOLOCK) ON ic.[object_id] = i.[object_id]
                                                AND ic.[stats_id] = i.[stats_id]
                                                AND COL_NAME(ic.[object_id], ic.column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
    WHERE ic.column_id IS NOT NULL
       OR i.filter_definition LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'
  
  RAISERROR('Drop Statistics Referencing Modified Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping statistic ' + id.[Schema] + '.' + id.[TableName] + '.[' + [StatName] + ']'', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'DROP STATISTICS ' + id.[Schema] + '.' + id.[TableName] + '.[' + [StatName] + '];', CHAR(13) + CHAR(10))
    FROM #StatisticsToDropForChanges id WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Identify Foreign Keys To Drop Based On Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #FKsToDropForChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName], FKName = fk.[name]
    INTO #FKsToDropForChanges
    FROM sys.foreign_key_columns fc WITH (NOLOCK)
    LEFT JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.object_id = fc.constraint_object_id
    JOIN #ColumnChanges cc WITH (NOLOCK) ON (OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) = fk.parent_object_id
                                         AND SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) = COL_NAME(fc.[parent_object_id], fc.parent_column_id))
                                         OR (OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) = fk.referenced_object_id
                                         AND SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) = COL_NAME(fc.[referenced_object_id], fc.referenced_column_id))
  
  RAISERROR('Drop Foreign Keys Referencing Modified Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping foreign Key ' + df.[Schema] + '.' + df.[TableName] + '.' + df.[FKName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + df.[Schema] + '.' + df.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + df.[FKName] + ';', CHAR(13) + CHAR(10))
    FROM #FKsToDropForChanges df WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Defaults To Drop Based On Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #DefaultsToDropForChanges
  SELECT cc.[Schema], cc.[TableName], DefaultName = dc.[name]
    INTO #DefaultsToDropForChanges
    FROM sys.default_constraints dc WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON dc.[parent_object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                                        AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
  
  RAISERROR('Drop Defaults Referencing Modified Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping default ' + dd.[Schema] + '.' + dd.[TableName] + '.' + dd.[DefaultName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + dd.[Schema] + '.' + dd.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + dd.[DefaultName] + ';', CHAR(13) + CHAR(10))
    FROM #DefaultsToDropForChanges dd WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Check Constraints To Drop Based On Column Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ChecksToDropForChanges
  SELECT cc.[Schema], cc.[TableName], CheckName = ck.[name]
    INTO #ChecksToDropForChanges
    FROM sys.check_constraints ck WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON ck.[parent_object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                                        AND ((ck.parent_column_id <> 0 AND COL_NAME(ck.parent_object_id, ck.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName))
                                          OR (ck.parent_column_id = 0 AND ck.[definition] LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'))
  
  RAISERROR('Drop Check Constraints Referencing Modified Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping check constraint ' + fc.[Schema] + '.' + fc.[TableName] + '.' + fc.CheckName + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + fc.[Schema] + '.' + fc.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + fc.CheckName + ';', CHAR(13) + CHAR(10))
    FROM #ChecksToDropForChanges fc WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Modified Computed Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping columns from ' + T.[Schema] + '.' + T.[Name] + ' (' + MessageColumns + ')'', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' DROP ' + ScriptColumns + ';', CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name], 
                 ScriptColumns = (SELECT STRING_AGG('COLUMN ' + [ColumnName], ', ') WITHIN GROUP (ORDER BY cc.[ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.MustDropAndRecreate = 1),
                 MessageColumns = (SELECT STRING_AGG([ColumnName], ', ') WITHIN GROUP (ORDER BY cc.[ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.MustDropAndRecreate = 1)
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 0
              AND EXISTS (SELECT * FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.MustDropAndRecreate = 1)) T
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Columns No Longer Part of The Product Definition', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping columns from ' + T.[Schema] + '.' + T.[Name] + ' (' + MessageColumns + ')'', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' DROP ' + ScriptColumns + ';', CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name],
                 ScriptColumns = (SELECT STRING_AGG('COLUMN ' + [ColumnName], ', ') WITHIN GROUP (ORDER BY [ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.DropOnly = 1),
                 MessageColumns = (SELECT STRING_AGG([ColumnName], ', ') WITHIN GROUP (ORDER BY [ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.DropOnly = 1)
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 0
              AND EXISTS (SELECT * FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.DropOnly = 1)) T
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  UPDATE c
    SET NewColumn = 1
    FROM #Columns c
    WHERE EXISTS (SELECT * FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = c.[Schema] AND cc.[TableName] = c.[TableName] and cc.ColumnName = c.ColumnName AND cc.MustDropAndRecreate = 1)
  
  RAISERROR('Add New Tables', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding new table ' + T.[Schema] + '.' + T.[Name] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'CREATE TABLE ' + T.[Schema] + '.' + T.[Name] + ' (' + ScriptColumns + ')' + 
                             CASE WHEN t.[CompressionType] IN ('NONE', 'ROW', 'PAGE') THEN ' WITH (DATA_COMPRESSION=' + t.[CompressionType] + ')' ELSE '' END + ';', CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name], t.[CompressionType],
                 ScriptColumns = (SELECT STRING_AGG([ColumnScript], ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name])
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 1) T
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add missing ProductName extended property to tables', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('EXEC sp_addextendedproperty @name = N''ProductName'', @value = ''' + @ProductName + ''', ' +
                                                         '@level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', ' +
                                                         '@level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''';', CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * FROM #TableProperties tp WITH (NOLOCK) WHERE t.[Schema] = tp.[Schema] AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName AND tp.PropertyName = 'ProductName')
      AND OBJECT_ID(t.[Schema] + '.' + t.[Name]) IS NOT NULL  -- and the table physically exists
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add New Physical Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding new columns to ' + T.[Schema] + '.' + T.[Name] + ' (' + ColumnMessage + ')'', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD ' + ColumnScripts + ';', CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name],
                 ColumnScripts = (SELECT STRING_AGG([ColumnScript], ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''),
                 ColumnMessage = (SELECT STRING_AGG([ColumnName], ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 0
              AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')) T
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Detect Default Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #DefaultChanges
  SELECT C.[Schema], C.[TableName], C.[ColumnName],
         [DefaultName] = (SELECT [Name] 
                            FROM sys.default_constraints dc WITH (NOLOCK)
                            WHERE dc.parent_object_id = OBJECT_ID(c.[Schema] + '.' + c.[TableName]) 
                              AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(C.[ColumnName]))
    INTO #DefaultChanges
    FROM #Tables T WITH (NOLOCK)
    JOIN #Columns c WITH (NOLOCK) ON C.[Schema] = T.[Schema] 
                                 AND C.[TableName] = T.[Name]
                                 AND C.[NewColumn] = 0
    JOIN INFORMATION_SCHEMA.COLUMNS ic ON ic.TABLE_SCHEMA = SchemaSmith.fn_StripBracketWrapping(C.[Schema])
                                      AND ic.TABLE_NAME = SchemaSmith.fn_StripBracketWrapping(C.[TableName])
                                      AND ic.COLUMN_NAME = SchemaSmith.fn_StripBracketWrapping(C.[ColumnName])
    WHERE t.NewTable = 0
      AND SchemaSmith.fn_StripParenWrapping(ic.COLUMN_DEFAULT) <> ISNULL(c.[Default], 'NULL')
  
  RAISERROR('Drop Modified Defaults', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping default ' + dc.[Schema] + '.' + dc.[TableName] + '.' + dc.[DefaultName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + dc.[Schema] + '.' + dc.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + dc.[DefaultName] + ';', CHAR(13) + CHAR(10))
    FROM #DefaultChanges dc WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing Foreign Keys', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingFKs
  SELECT t.[Schema], [TableName] = t.[Name],
         FKName = fk.[Name],
         FKScript = '(' + (SELECT STRING_AGG('[' + COL_NAME(fc.[parent_object_id], fc.parent_column_id) + ']', ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                             FROM sys.foreign_key_columns fc WITH (NOLOCK)
                             WHERE fk.[object_id] = fc.[constraint_object_id]) + ')' +
                    ' REFERENCES [' + OBJECT_SCHEMA_NAME(referenced_object_id) + '].[' + OBJECT_NAME(referenced_object_id) + '] ' +
                    '(' + (SELECT STRING_AGG('[' + COL_NAME(fc.[referenced_object_id], fc.referenced_column_id) + ']', ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                             FROM sys.foreign_key_columns fc WITH (NOLOCK)
                             WHERE fk.[object_id] = fc.[constraint_object_id]) + ')' +
                    CASE WHEN fk.update_referential_action = 1 THEN ' ON UPDATE CASCADE' ELSE '' END +
                    CASE WHEN fk.delete_referential_action = 1 THEN ' ON DELETE CASCADE' ELSE '' END
    INTO #ExistingFKs
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.parent_object_id = OBJECT_ID(t.[Schema] + '.' + t.[Name]) 
    WHERE t.NewTable = 0

  RAISERROR('Detect Foreign Key Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #FKChanges
  SELECT ek.[Schema], ek.[TableName], ek.[FKName]
    INTO #FKChanges
    FROM #ExistingFKs ek WITH (NOLOCK)
    JOIN #ForeignKeys fk WITH (NOLOCK) ON ek.[TableName] = fk.[TableName]
                                      AND ek.[Schema] = fk.[Schema]
                                      AND ek.[FKName] = SchemaSmith.fn_StripBracketWrapping(fk.[KeyName])
    WHERE ek.FKScript <> '(' + [Columns] + ') REFERENCES ' + [RelatedTableSchema] + '.' + [RelatedTable] + ' (' + [RelatedColumns] + ')' +
                         CASE WHEN fk.[CascadeOnUpdate] = 1 THEN ' ON UPDATE CASCADE' ELSE '' END +
                         CASE WHEN fk.[CascadeOnDelete] = 1 THEN ' ON DELETE CASCADE' ELSE '' END
  
  RAISERROR('Drop Modified Foreign Keys', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping Foreign Key ' + fc.[Schema] + '.' + fc.[TableName] + '.' + fc.[FKName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + fc.[Schema] + '.' + fc.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + fc.[FKName] + ';', CHAR(13) + CHAR(10))
    FROM #FKChanges fc WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing Statistics Definitions', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingStats
  SELECT t.[Schema], [TableName] = t.[Name], [StatsName] = si.[Name],
         StatisticScript = 'CREATE STATISTICS ' +
                           '[' + si.[Name] + '] ON ' + t.[Schema] + '.' + t.[Name] + ' (' +
                           (SELECT STRING_AGG('[' + COL_NAME(ic.[object_id], ic.column_id) + ']', ',') WITHIN GROUP (ORDER BY ic.stats_column_id)
                              FROM sys.stats_columns ic WITH (NOLOCK)
                              WHERE si.[object_id] = ic.[object_id] AND si.stats_id = ic.stats_id) + ')' +
                           CASE WHEN si.has_filter = 1 THEN ' WHERE ' + SchemaSmith.fn_StripParenWrapping(si.filter_definition) ELSE '' END 
    INTO #ExistingStats 
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.stats si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                   AND auto_created = 0
                                   AND user_created = 1
                                   AND is_temporary = 0
                                   AND si.[Name] NOT LIKE 'stat[_]%'
                                   AND si.[Name] NOT LIKE 'hind[_]%'
    WHERE t.NewTable = 0
  
  RAISERROR('Detect Statistics Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #StatsChanges
  SELECT s.[Schema], s.[TableName], s.[StatisticName]
    INTO #StatsChanges
    FROM #Statistics s WITH (NOLOCK)
    JOIN #ExistingStats es WITH (NOLOCK) ON s.[Schema] = es.[Schema]
                                        AND s.[TableName] = es.[TableName]
                                        AND SchemaSmith.fn_StripBracketWrapping(s.[StatisticName]) = es.[StatsName]
    WHERE es.StatisticScript <> 'CREATE STATISTICS ' + s.[StatisticName] + ' ON ' + s.[Schema] + '.' + s.[TableName] + ' (' + s.[Columns] + ')' +
                                CASE WHEN RTRIM(ISNULL(s.[FilterExpression], '')) <> '' THEN ' WHERE ' + s.[FilterExpression] ELSE '' END

  RAISERROR('Drop Modified Statistics', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping statistics ' + sc.[Schema] + '.' + sc.[TableName] + '.' + sc.[StatisticName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'DROP STATISTICS ' + sc.[Schema] + '.' + sc.[TableName] + '.' + sc.[StatisticName] + ';', CHAR(13) + CHAR(10))
    FROM #StatsChanges sc WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing Check Constraints', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingCheckConstraints
  SELECT t.[Schema], [TableName] = t.[Name], [CheckName] = ck.[name], 
         [CheckColumn] = CASE WHEN ck.parent_column_id <> 0 THEN COL_NAME(ck.parent_object_id, ck.parent_column_id) ELSE NULL END,
         [CheckDefinition] = SchemaSmith.fn_StripParenWrapping(ck.[definition])
    INTO #ExistingCheckConstraints
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.check_constraints ck WITH (NOLOCK) ON ck.[parent_object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
  
  RAISERROR('Detect Column Level Check Constraint Changes', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #CheckChanges
  SELECT ec.[Schema], ec.[TableName], ec.[CheckName]
    INTO #CheckChanges
    FROM #ExistingCheckConstraints ec WITH (NOLOCK)
    JOIN #Columns c WITH (NOLOCK) ON ec.[Schema] = c.[Schema]
                                 AND ec.[TableName] = c.[TableName]
                                 AND ec.[CheckColumn] = SchemaSmith.fn_StripBracketWrapping(c.[ColumnName])
    WHERE ec.[CheckColumn] IS NOT NULL
      AND ec.[CheckDefinition] <> ISNULL(c.[CheckExpression], '')
      AND NOT EXISTS (SELECT * 
                        FROM #CheckConstraints cc WITH (NOLOCK) 
                        WHERE ec.[Schema] = cc.[Schema]
                          AND ec.[TableName] = cc.[TableName]
                          AND ec.[CheckName] = SchemaSmith.fn_StripBracketWrapping(cc.[ConstraintName]))

  RAISERROR('Detect Table Level Check Constraint Changes', 10, 1) WITH NOWAIT
  INSERT #CheckChanges ([Schema], [TableName], [CheckName])
    SELECT ec.[Schema], ec.[TableName], ec.[CheckName]
      FROM #ExistingCheckConstraints ec WITH (NOLOCK)
      JOIN #CheckConstraints cc WITH (NOLOCK) ON ec.[Schema] = cc.[Schema]
                                             AND ec.[TableName] = cc.[TableName]
                                             AND ec.[CheckName] = SchemaSmith.fn_StripBracketWrapping(cc.[ConstraintName])
      WHERE ec.[CheckDefinition] <> cc.[Expression]
  
  RAISERROR('Drop Modified Check Constraints', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping check constraint ' + cc.[Schema] + '.' + cc.[TableName] + '.' + cc.[CheckName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + cc.[Schema] + '.' + cc.[TableName] + ' DROP CONSTRAINT IF EXISTS ' + cc.[CheckName] + ';', CHAR(13) + CHAR(10))
    FROM #CheckChanges cc WITH (NOLOCK)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Alter Modified Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Altering Column ' + cc.[Schema] + '.' + cc.[TableName] + '.' + cc.[ColumnName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + cc.[Schema] + '.' + cc.[TableName] + ' ALTER COLUMN ' + cc.[ColumnName] + ' ' + [ColumnScript] + ';', CHAR(13) + CHAR(10))
        FROM #ColumnChanges cc WITH (NOLOCK)
        WHERE [MustDropAndRecreate] = 0
          AND [DropOnly] = 0
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add New Computed Columns', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding new columns to ' + T.[Schema] + '.' + T.[Name] + ' (' + MessageColumns + ')'', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD ' + ScriptColumns + ';', CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name],
                 ScriptColumns = (SELECT STRING_AGG(c.[ColumnScript], ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) <> ''),
                 MessageColumns = (SELECT STRING_AGG(c.[ColumnName], ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) <> '')
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 0
              AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) <> '')) T
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Existing Clustered Index Conflicts', 10, 1) WITH NOWAIT
  DROP TABLE IF EXISTS #MissingClusteredIndexTables
  SELECT DISTINCT i.[Schema], i.[TableName]
    INTO #MissingClusteredIndexTables
    FROM #Indexes i WITH (NOLOCK)
    WHERE i.[Clustered] = 1
      AND NOT EXISTS (SELECT * 
                        FROM sys.indexes si WITH (NOLOCK)
                        WHERE si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName]) 
                          AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName]))
  
  RAISERROR('Drop Conflicting Clustered Index', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping ' + CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + mct.[Schema] + '.' + mct.[TableName] + '.' + si.[Name] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1
                                  THEN 'ALTER TABLE ' + mct.[Schema] + '.' + mct.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + si.[Name] + '];'
                                  ELSE 'DROP INDEX IF EXISTS ' + mct.[Schema] + '.' + mct.[TableName] + '.[' + si.[Name] + '];'
                                  END, CHAR(13) + CHAR(10))
    FROM #MissingClusteredIndexTables mct WITH (NOLOCK)
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(mct.[Schema] + '.' + mct.[TableName])
                                     AND si.[type] IN (1, 5)
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add Missing Indexes', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Creating ' + CASE WHEN i.PrimaryKey = 1 OR i.UniqueConstraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             CASE WHEN i.PrimaryKey = 1 OR i.UniqueConstraint = 1
                                  THEN 'ALTER TABLE ' + i.[Schema] + '.' + i.[TableName] + ' ADD CONSTRAINT ' + i.[IndexName] +
                                       CASE WHEN i.PrimaryKey = 1 THEN ' PRIMARY KEY ' WHEN i.UniqueConstraint = 1 THEN ' UNIQUE ' END +
                                       CASE WHEN i.[Clustered] =  1 THEN '' ELSE 'NON' END + 'CLUSTERED (' + i.IndexColumns + ')' +
                                       CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE')
                                            THEN ' WITH (DATA_COMPRESSION=' + i.[CompressionType] + ')'
                                            ELSE '' END
                                  ELSE 'CREATE ' + 
                                       CASE WHEN i.[Unique] = 1 THEN 'UNIQUE ' ELSE '' END +
                                       CASE WHEN i.[Clustered] =  1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                                       'INDEX ' + i.[IndexName] +
                                       ' ON ' + i.[Schema] + '.' + i.[TableName] + '(' + i.IndexColumns + ')' +
                                       CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END +
                                       CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
					                   CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE')
                                              OR ISNULL(i.[FillFactor], 100) NOT IN (0, 100)
                                            THEN ' WITH (' +
                                                 CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE') THEN 'DATA_COMPRESSION=' + i.[CompressionType] ELSE '' END +
                                                 CASE WHEN ISNULL(i.[FillFactor], 100) NOT IN (0, 100) 
                                                      THEN CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE') THEN ', ' ELSE '' END +
                                                           'FILLFACTOR = ' + CAST(i.[FillFactor] AS VARCHAR(20)) 
                                                      ELSE '' END +
							                     ')'
                                            ELSE '' END
                                  END + ';', CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY i.[Schema], i.[TableName], CASE WHEN i.[Clustered] =  1 THEN 0 ELSE 1 END, i.[IndexName])
    FROM #Indexes i WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * 
                        FROM sys.indexes si WITH (NOLOCK)
                        WHERE si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName]) 
                          AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName]))    
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Turn on Temporal Tracking for tables defined as temporal', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Turn ON Temporal Tracking for ' + T.[Schema] + '.' + T.[Name] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD [ValidFrom] DATETIME2(7) GENERATED ALWAYS AS ROW START NOT NULL DEFAULT ''0001-01-01 00:00:00.0000000'', ' +
                                                                                 '[ValidTo] DATETIME2(7) GENERATED ALWAYS AS ROW END NOT NULL DEFAULT ''9999-12-31 23:59:59.9999999'', ' +
                                                                                 'PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo);' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + T.[Schema] + '.[' + SchemaSmith.fn_StripBracketWrapping(T.[Name]) + '_Hist]));', CHAR(13) + CHAR(10))
    FROM #Tables T WITH (NOLOCK)
    WHERE t.IsTemporal = 1
      AND OBJECTPROPERTY(OBJECT_ID([Schema] + '.' + [Name]), 'TableTemporalType') = 0
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add missing ProductName extended property to indexes', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('EXEC sp_addextendedproperty @name = N''ProductName'', @value = ''' + @ProductName + ''', ' +
                                                         '@level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', ' +
                                                         '@level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', ' +
                                                         '@level2type = N''Index'', @level2name = ''' + SchemaSmith.fn_StripBracketWrapping(i.IndexName) + ''';', CHAR(13) + CHAR(10))
    FROM #Indexes i WITH (NOLOCK)
    JOIN #Tables t WITH (NOLOCK) ON t.[Schema] = i.[Schema] AND t.[Name] = i.[TableName]
    WHERE INDEXPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name]), SchemaSmith.fn_StripBracketWrapping(i.IndexName), 'IndexID') IS NOT NULL
      AND NOT EXISTS (SELECT * FROM #IndexProperties ip WITH (NOLOCK) WHERE i.[Schema] = ip.[Schema] AND i.TableName = ip.TableName AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = ip.IndexName AND ip.PropertyName = 'ProductName')
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add Missing Statistics', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Creating statistics ' + s.[Schema] + '.' + s.[TableName] + '.' + s.[StatisticName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'CREATE STATISTICS ' + s.[StatisticName] + ' ON ' + s.[Schema] + '.' + s.[TableName] + ' (' + s.[Columns] + ')' +
                             CASE WHEN RTRIM(ISNULL(s.[FilterExpression], '')) <> '' THEN ' WHERE ' + s.[FilterExpression] ELSE '' END +
                             ' WITH SAMPLE ' + CAST(ISNULL(s.[SampleSize], 100) AS VARCHAR(20)) + ' PERCENT;', CHAR(13) + CHAR(10))
    FROM #Statistics s WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * 
                        FROM sys.stats ss WITH (NOLOCK)
                        WHERE ss.[object_id] = OBJECT_ID(s.[Schema] + '.' + s.[TableName]) 
                          AND ss.[name] = SchemaSmith.fn_StripBracketWrapping(s.[StatisticName]))
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add Missing Defaults', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Altering Column ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + c.[Schema] + '.' + c.[TableName] + ' ADD DEFAULT ' + c.[Default] + ' FOR ' + c.[ColumnName] + ';', CHAR(13) + CHAR(10))
    FROM #Columns c WITH (NOLOCK)
    WHERE RTRIM(ISNULL(c.[Default], '')) <> ''
      AND NOT EXISTS (SELECT * 
                        FROM sys.default_constraints dc WITH (NOLOCK)
                        WHERE dc.[parent_object_id] = OBJECT_ID(c.[Schema] + '.' + c.[TableName]) 
                          AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(c.ColumnName))
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add Missing Check Constraints', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding check constraint ' + cc.[Schema] + '.' + cc.[TableName] + '.' + cc.[ConstraintName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + cc.[Schema] + '.' + cc.[TableName] + ' ADD CONSTRAINT ' + cc.[ConstraintName] + ' CHECK (' + cc.[Expression] + ');', CHAR(13) + CHAR(10))
    FROM #CheckConstraints cc WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * 
                        FROM sys.check_constraints sc WITH (NOLOCK)
                        WHERE sc.[parent_object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                          AND sc.[name] = SchemaSmith.fn_StripBracketWrapping(cc.[ConstraintName]))
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding check constrain to column ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + c.[Schema] + '.' + c.[TableName] + ' ADD CHECK (' + c.[CheckExpression] + ');', CHAR(13) + CHAR(10))
    FROM #Columns c WITH (NOLOCK)
    WHERE RTRIM(ISNULL(c.[CheckExpression], '')) <> ''
      AND NOT EXISTS (SELECT * 
                        FROM sys.check_constraints sc WITH (NOLOCK)
                        WHERE sc.[parent_object_id] = OBJECT_ID(c.[Schema] + '.' + c.[TableName]) 
                          AND COL_NAME(sc.parent_object_id, sc.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]))
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add Missing Foreign Keys', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding foreign key ' + f.[Schema] + '.' + f.[TableName] + '.' + f.[KeyName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'ALTER TABLE ' + f.[Schema] + '.' + f.[TableName] + ' ADD CONSTRAINT ' + f.[KeyName] + ' FOREIGN KEY ' + 
                             '(' + f.[Columns] + ') REFERENCES ' + [RelatedTableSchema] + '.' + f.[RelatedTable] + ' (' + [RelatedColumns] + ')' +
                             CASE WHEN f.[CascadeOnUpdate] = 1 THEN ' ON UPDATE CASCADE' ELSE '' END +
                             CASE WHEN f.[CascadeOnDelete] = 1 THEN ' ON DELETE CASCADE' ELSE '' END + ';', CHAR(13) + CHAR(10))
    FROM #ForeignKeys f WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT *
                        FROM sys.foreign_keys sf WITH (NOLOCK)
                        WHERE sf.[parent_object_id] = OBJECT_ID(f.[Schema] + '.' + f.[TableName]) 
                          AND sf.[name] = SchemaSmith.fn_StripBracketWrapping(f.[KeyName]))
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Modified or Removed FullText Indexes', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Dropping fulltext index on ' + ei.[Schema] + '.' + ei.[TableName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'DROP FULLTEXT INDEX ON ' + ei.[Schema] + '.' + ei.[TableName] + ';', CHAR(13) + CHAR(10))
    FROM #ExistingFullTextIndexes ei WITH (NOLOCK)
    LEFT JOIN #FullTextIndexes fi WITH (NOLOCK) ON fi.[Schema] = ei.[Schema]
                                               AND fi.[TableName] = ei.[TableName]
    JOIN sys.fulltext_indexes ft WITH (NOLOCK) ON ft.[object_id] = OBJECT_ID(ei.[Schema] + '.' + ei.[TableName])
    WHERE RTRIM(ISNULL(fi.[Columns], '')) <> RTRIM(ISNULL(ei.[Columns], ''))
       OR SchemaSmith.fn_StripBracketWrapping(fi.[FullTextCatalog]) <> SchemaSmith.fn_StripBracketWrapping(ei.[FullTextCatalog])
       OR SchemaSmith.fn_StripBracketWrapping(fi.[KeyIndex]) <> SchemaSmith.fn_StripBracketWrapping(ei.[KeyIndex])
       OR fi.[ChangeTracking] <> ei.[ChangeTracking]
       OR RTRIM(ISNULL(fi.[StopList], '')) <> RTRIM(ISNULL(ei.[StopList], ''))
       OR fi.[TableName] IS NULL
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add Missing FullText Indexes', 10, 1) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG('RAISERROR(''  Adding fulltext index on ' + fi.[Schema] + '.' + fi.[TableName] + ''', 10, 1) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                             'CREATE FULLTEXT INDEX ON ' + fi.[Schema] + '.' + fi.[TableName] + ' (' + [Columns] + ') KEY INDEX ' + [KeyIndex] + ' ON ' + [FullTextCatalog] + 
                             ' WITH CHANGE_TRACKING = ' + [ChangeTracking] +
                             CASE WHEN RTRIM(ISNULL(fi.[StopList], '')) <> '' THEN ', STOPLIST = ' + [StopList] ELSE '' END + ';', CHAR(13) + CHAR(10))
    FROM #FullTextIndexes fi WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * FROM sys.fulltext_indexes ft WITH (NOLOCK) WHERE ft.[object_id] = OBJECT_ID(fi.[Schema] + '.' + fi.[TableName]))
  IF @WhatIf = 1 PRINT @v_SQL ELSE EXEC(@v_SQL)
  
  SET NOCOUNT OFF
END TRY
BEGIN CATCH
  THROW
END CATCH
