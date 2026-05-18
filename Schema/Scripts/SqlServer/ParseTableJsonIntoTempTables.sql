-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

  DECLARE @v_SQL NVARCHAR(MAX) = ''
  SET NOCOUNT ON
  RAISERROR('Parse Tables from Json', 10, 100) WITH NOWAIT

  -- I5: missing/blank [Schema] is a programmer error after slice-1's SchemaDefaultResolver.
  -- The canonical Load path fills Schema with the platform default ('dbo' / 'public') or the
  -- {{SchemaName}} token; a blank value here means a caller built the JSON without going
  -- through Template.Load (or a downstream substitution swallowed the token). Silently
  -- defaulting to dbo here is data-loss-equivalent for schema templates — fail loud.
  IF EXISTS (SELECT 1 FROM OPENJSON(@TableDefinitions) WITH ([Schema] NVARCHAR(500) '$.Schema', [Name] NVARCHAR(500) '$.Name')
                 WHERE NULLIF(RTRIM(ISNULL([Schema], '')), '') IS NULL)
  BEGIN
    DECLARE @v_BadTable NVARCHAR(500) =
      (SELECT TOP 1 ISNULL([Name], '<unnamed>') FROM OPENJSON(@TableDefinitions) WITH ([Schema] NVARCHAR(500) '$.Schema', [Name] NVARCHAR(500) '$.Name')
         WHERE NULLIF(RTRIM(ISNULL([Schema], '')), '') IS NULL);
    DECLARE @v_Msg NVARCHAR(2000) = 'Table JSON is missing Schema for table ''' + @v_BadTable + '''. ' +
      'Schema must be populated before reaching ParseTableJsonIntoTempTables — this is a programmer error. ' +
      'In production the SchemaDefaultResolver fills Schema with the platform default or the {{SchemaName}} token; ' +
      'a blank value here means a caller bypassed Template.Load or substituted the token away.';
    THROW 51000, @v_Msg, 1;
  END

  DROP TABLE IF EXISTS #TableDefinitions
  SELECT [Schema] = SchemaSmith.fn_SafeBracketWrap([Schema]), [Name] = SchemaSmith.fn_SafeBracketWrap([Name]), [CompressionType] = ISNULL(NULLIF(RTRIM([CompressionType]), ''), 'NONE'),
         [IsTemporal] = ISNULL([IsTemporal], 0), [UpdateFillFactor] = ISNULL([UpdateFillFactor], 0),
         [Indexes], [XmlIndexes], [Columns], [Statistics], [FullTextIndex], [ForeignKeys], [CheckConstraints],
         [ShouldApplyExpression], [EnableCDC] = ISNULL([EnableCDC], 0), [OldName] = SchemaSmith.fn_SafeBracketWrap([OldName])
    INTO #TableDefinitions
    FROM OPENJSON(@TableDefinitions) WITH (
      [Schema] NVARCHAR(500) '$.Schema',
      [Name] NVARCHAR(500) '$.Name',
      [CompressionType] NVARCHAR(100) '$.CompressionType',
      [IsTemporal] BIT '$.IsTemporal',
      [UpdateFillFactor] BIT '$.UpdateFillFactor',
      [OldName] NVARCHAR(500) '$.OldName',
	  [Indexes] NVARCHAR(MAX) '$.Indexes' AS JSON,
	  [XmlIndexes] NVARCHAR(MAX) '$.XmlIndexes' AS JSON,
      [Columns] NVARCHAR(MAX) '$.Columns' AS JSON,
	  [Statistics] NVARCHAR(MAX) '$.Statistics' AS JSON,
	  [FullTextIndex] NVARCHAR(MAX) '$.FullTextIndex' AS JSON,
      [ForeignKeys] NVARCHAR(MAX) '$.ForeignKeys' AS JSON,
      [CheckConstraints] NVARCHAR(MAX) '$.CheckConstraints' AS JSON,
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression',
      [EnableCDC] BIT '$.EnableCDC'
      ) t;
  
  -- Identify Tables to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #TableDefinitions WHERE [Schema] = ''' + [Schema] + ''' AND [Name] = ''' + [Name] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #TableDefinitions WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  DROP TABLE IF EXISTS #Tables
  SELECT [Schema], [Name], [CompressionType], [IsTemporal], [UpdateFillFactor], [EnableCDC], [OldName],
         CONVERT(BIT, CASE WHEN OBJECT_ID([Schema] + '.' + [Name], 'U') IS NULL AND OBJECT_ID([Schema] + '.' + [OldName], 'U') IS NULL THEN 1 ELSE 0 END) AS NewTable
    INTO #Tables
    FROM #TableDefinitions WITH (NOLOCK);
  
  RAISERROR('Parse Columns from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Columns
  SELECT t.[Schema], t.[Name] AS [TableName], [ColumnName] = SchemaSmith.fn_SafeBracketWrap(c.[ColumnName]),
         -- Canonicalize the JSON DataType so the live-vs-declared comparison
         -- in ModifiedTableQuench (which builds USER_TYPE + DATETIME_PRECISION
         -- as e.g. "DATETIME2(7)") matches a JSON-declared "DATETIME2" without
         -- explicit precision. SQL Server defaults DATETIME2 / TIME /
         -- DATETIMEOFFSET to precision 7 — without canonicalization, every
         -- re-quench against a column declared with the default precision sees
         -- false drift and emits a destructive ALTER COLUMN that cascades to
         -- any dependent computed columns and indexes.
         [DataType] = CASE WHEN UPPER(LTRIM(RTRIM(REPLACE(c.[DataType], 'ROWVERSION', 'TIMESTAMP')))) IN ('DATETIME2', 'TIME', 'DATETIMEOFFSET')
                            THEN UPPER(LTRIM(RTRIM(REPLACE(c.[DataType], 'ROWVERSION', 'TIMESTAMP')))) + '(7)'
                            ELSE REPLACE(c.[DataType], 'ROWVERSION', 'TIMESTAMP') END,
         [Nullable] = ISNULL(c.[Nullable], 0),
         c.[Default], c.[CheckExpression], c.[ComputedExpression], [Persisted] = ISNULL(c.[Persisted], 0),
         [Sparse] = ISNULL(c.[Sparse], 0), [Collation] = RTRIM(ISNULL(c.[Collation], '')), [DataMaskFunction] = RTRIM(ISNULL(c.[DataMaskFunction], '')), 
         [EncryptionType] = ISNULL(c.[EncryptionType], 'NONE'), [EncryptionKey] = RTRIM(ISNULL(c.[EncryptionKey], '')), [EncryptionAlgorithm] = RTRIM(ISNULL(c.[EncryptionAlgorithm], '')),
         [OldName] = SchemaSmith.fn_SafeBracketWrap(c.[OldName]),
         CONVERT(BIT, CASE WHEN (RTRIM(ISNULL([ComputedExpression], '')) <> '' OR NOT EXISTS (SELECT * FROM #Tables x WHERE x.[Name] = t.[Name] AND x.[Schema] = t.[Schema] AND x.NewTable = 1))
                            AND COLUMNPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name], 'U'), SchemaSmith.fn_StripBracketWrapping([ColumnName]), 'ColumnId') IS NULL
                            -- Not a new column if it exists by current name in the table being renamed from (table rename scenario)
                            AND COLUMNPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[OldName], 'U'), SchemaSmith.fn_StripBracketWrapping([ColumnName]), 'ColumnId') IS NULL
                            -- Not a new column if the column's own OldName exists (column rename scenario)
                            AND COLUMNPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name], 'U'), SchemaSmith.fn_StripBracketWrapping(c.[OldName]), 'ColumnId') IS NULL
                           THEN 1 ELSE 0 END) AS NewColumn,
         SchemaSmith.fn_SafeBracketWrap(c.[ColumnName]) + ' ' +
         -- For computed columns only the expression is needed
         CASE WHEN RTRIM(ISNULL([ComputedExpression], '')) <> '' THEN 'AS (' + ComputedExpression + ')' + CASE WHEN ISNULL(c.[Persisted], 0) = 1 THEN ' PERSISTED' ELSE '' END
                                                                                                     + CASE WHEN ISNULL(c.[Persisted], 0) = 1 AND ISNULL(c.[Nullable], 1) = 0 THEN ' NOT NULL' ELSE '' END
              -- Otherwise build the column definition
              ELSE UPPER(REPLACE(c.[DataType], 'ROWVERSION', 'TIMESTAMP')) + 
                   CASE WHEN RTRIM(ISNULL([Collation], '')) NOT IN ('IGNORE', '') THEN ' COLLATE ' + [Collation] ELSE '' END +                   
                   CASE WHEN ISNULL([Sparse], 0) = 1 THEN ' SPARSE' ELSE '' END +                   
                   CASE WHEN RTRIM(ISNULL([DataMaskFunction], '')) <> '' THEN ' MASKED WITH (FUNCTION = ''' + [DataMaskFunction] + ''')' ELSE '' END +
                   CASE WHEN RTRIM(ISNULL([EncryptionType], 'NONE')) <> 'NONE'
                        THEN ' ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = ' + [EncryptionKey] + ', ENCRYPTION_TYPE = ' + [EncryptionType] + ', ALGORITHM = ''' + [EncryptionAlgorithm] + ''')'
                        ELSE '' END +
                   CASE WHEN ISNULL(Nullable, 0) = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                   CASE WHEN RTRIM(ISNULL([Default], '')) <> '' THEN ' DEFAULT ' + [Default] ELSE '' END
              END AS [ColumnScript],
         c.[ShouldApplyExpression]
    INTO #Columns
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(Columns) WITH (
      [ColumnName] NVARCHAR(500) '$.Name',
      [DataType] NVARCHAR(100) '$.DataType',
      [Nullable] BIT '$.Nullable',
      [Default] NVARCHAR(MAX) '$.Default',
      [CheckExpression] NVARCHAR(MAX) '$.CheckExpression',
      [ComputedExpression] NVARCHAR(MAX) '$.ComputedExpression',
      [Persisted] BIT '$.Persisted',
      [Sparse] BIT '$.Sparse',
      [Collation] NVARCHAR(500) '$.Collation',
      [DataMaskFunction] NVARCHAR(500) '$.DataMaskFunction',
      [EncryptionType] NVARCHAR(100) '$.EncryptionType',
      [EncryptionKey] NVARCHAR(500) '$.EncryptionKey',
      [EncryptionAlgorithm] NVARCHAR(500) '$.EncryptionAlgorithm',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression',
      [OldName] NVARCHAR(500) '$.OldName'
      ) c;

  -- Identify Columns to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #Columns WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND [ColumnName] = ''' + [ColumnName] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Columns WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  -- Don't try to apply tables without columns
  DELETE FROM #Tables
    WHERE NOT EXISTS (SELECT * FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = #Tables.[Schema] AND C.[TableName] = #Tables.[Name])
  DELETE FROM #TableDefinitions
    WHERE NOT EXISTS (SELECT * FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = #TableDefinitions.[Schema] AND C.[TableName] = #TableDefinitions.[Name])

  RAISERROR('Parse Indexes from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Indexes
  SELECT t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), [CompressionType] = ISNULL(NULLIF(RTRIM(i.[CompressionType]), ''), 'NONE'), [PrimaryKey] = ISNULL(i.[PrimaryKey], 0), 
         [Unique] = COALESCE(NULLIF(i.[Unique], 0), NULLIF(i.[PrimaryKey], 0), i.[UniqueConstraint], 0),
         [UniqueConstraint] = ISNULL(i.[UniqueConstraint], 0), [Clustered] = ISNULL(i.[Clustered], 0), [ColumnStore] = ISNULL(i.[ColumnStore], 0), [FillFactor] = ISNULL(NULLIF(i.[FillFactor], 0), 100),
         i.[FilterExpression], [UpdateFillFactor] = CONVERT(BIT, CASE WHEN @UpdateFillFactor = 1 OR t.[UpdateFillFactor] = 1 OR i.[UpdateFillFactor] = 1 THEN 1 ELSE 0 END),
         [IndexColumns] = (SELECT STRING_AGG(CAST(CASE WHEN RTRIM([value]) LIKE '% DESC' 
                                                       THEN SchemaSmith.fn_SafeBracketWrap(SUBSTRING(RTRIM([value]), 1, LEN(RTRIM([value])) - 5)) + ' DESC'
                                                       ELSE SchemaSmith.fn_SafeBracketWrap([value])
                                                       END AS NVARCHAR(MAX)), ',') 
                             FROM STRING_SPLIT(i.[IndexColumns], ',') 
                             WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [IncludeColumns] = (SELECT STRING_AGG(SchemaSmith.fn_SafeBracketWrap([value]), ',') WITHIN GROUP (ORDER BY SchemaSmith.fn_SafeBracketWrap([value]))
                               FROM STRING_SPLIT(i.[IncludeColumns], ',') 
                               WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         i.[ShouldApplyExpression]
    INTO #Indexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(Indexes) WITH (
      [IndexName] NVARCHAR(500) '$.Name',
      [CompressionType] NVARCHAR(100) '$.CompressionType',
      [PrimaryKey] BIT '$.PrimaryKey',
      [Unique] BIT '$.Unique',
	  [UniqueConstraint] BIT '$.UniqueConstraint',
      [Clustered] BIT '$.Clustered',
      [ColumnStore] BIT '$.ColumnStore',
      [FillFactor] TINYINT '$.FillFactor',
      [FilterExpression] NVARCHAR(MAX) '$.FilterExpression',
      [IndexColumns] NVARCHAR(MAX) '$.IndexColumns',
      [IncludeColumns] NVARCHAR(MAX) '$.IncludeColumns',
      [UpdateFillFactor] BIT '$.UpdateFillFactor',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression'
      ) i;
  
  -- Identify Indexes to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #Indexes WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND [IndexName] = ''' + [IndexName] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Indexes WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)
  
  RAISERROR('Parse XML Indexes from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexes
  SELECT t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), i.[IsPrimary],
         [Column] = SchemaSmith.fn_SafeBracketWrap(i.[Column]), [PrimaryIndex] = SchemaSmith.fn_SafeBracketWrap(i.[PrimaryIndex]),
         i.[SecondaryIndexType], i.[ShouldApplyExpression]
    INTO #XmlIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(XmlIndexes) WITH (
      [IndexName] NVARCHAR(500) '$.Name',
      [IsPrimary] BIT '$.IsPrimary',
      [Column] NVARCHAR(500) '$.Column',
      [PrimaryIndex] NVARCHAR(500) '$.PrimaryIndex',
	  [SecondaryIndexType] NVARCHAR(500) '$.SecondaryIndexType',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression'
      ) i;

  -- Identify XmlIndexes to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #XmlIndexes WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND [IndexName] = ''' + [IndexName] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #XmlIndexes WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)
  
  RAISERROR('Parse Foreign Keys from Json', 10, 100) WITH NOWAIT
  -- I5: missing/blank RelatedTableSchema is a programmer error after slice-1's resolver
  -- (which fills it from the platform default for regular templates or {{SchemaName}} for
  -- schema templates). Silent 'dbo' fallback here is data-loss-equivalent — fail loud.
  -- Implementation note: this check runs against the #ForeignKeys temp table AFTER the
  -- main FK parse below, not against the raw @TableDefinitions JSON. Running it earlier
  -- against the JSON ran into OPENJSON 'AS JSON' edge cases with single-object inputs
  -- (the kindling JSON for SchemaSmith's own bootstrap tables passes a single object).
  -- Post-parse the check is uniform across object / array inputs.

  DROP TABLE IF EXISTS #ForeignKeys
  SELECT t.[Schema], t.[Name] AS [TableName], [KeyName] = SchemaSmith.fn_SafeBracketWrap(f.[KeyName]),
         [RelatedTableSchema] = SchemaSmith.fn_SafeBracketWrap(f.[RelatedTableSchema]), [RelatedTable] = SchemaSmith.fn_SafeBracketWrap(f.[RelatedTable]),
         [Columns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') FROM STRING_SPLIT(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [RelatedColumns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') FROM STRING_SPLIT(f.[RelatedColumns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [DeleteAction] = ISNULL(NULLIF(RTRIM([DeleteAction]), ''), 'NO ACTION'),
         [UpdateAction] = ISNULL(NULLIF(RTRIM([UpdateAction]), ''), 'NO ACTION'),
         f.[ShouldApplyExpression]
    INTO #ForeignKeys
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(ForeignKeys) WITH (
      [KeyName] NVARCHAR(500) '$.Name',
      [Columns] NVARCHAR(MAX) '$.Columns',
      [RelatedTableSchema] NVARCHAR(500) '$.RelatedTableSchema',
      [RelatedTable] NVARCHAR(500) '$.RelatedTable',
      [RelatedColumns] NVARCHAR(MAX) '$.RelatedColumns',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression',
      [DeleteAction] NVARCHAR(20) '$.DeleteAction',
      [UpdateAction] NVARCHAR(20) '$.UpdateAction'
      ) f;

  -- I5: post-parse RelatedTableSchema check. fn_SafeBracketWrap(NULL) returns NULL
  -- (string concat with NULL yields NULL), and fn_SafeBracketWrap('') returns '[]'.
  -- Both sentinels indicate a blank input — fail loud rather than letting downstream
  -- code emit DDL against an unintended schema.
  IF EXISTS (SELECT 1 FROM #ForeignKeys WITH (NOLOCK)
               WHERE [RelatedTableSchema] IS NULL OR [RelatedTableSchema] IN ('[]', '[ ]', ''))
  BEGIN
    DECLARE @v_BadFk NVARCHAR(500) =
      (SELECT TOP 1 ISNULL([KeyName], '<unnamed>') FROM #ForeignKeys WITH (NOLOCK)
         WHERE [RelatedTableSchema] IS NULL OR [RelatedTableSchema] IN ('[]', '[ ]', ''));
    DECLARE @v_FkMsg NVARCHAR(2000) = 'Foreign key ''' + @v_BadFk + ''' is missing RelatedTableSchema. ' +
      'RelatedTableSchema must be populated before reaching ParseTableJsonIntoTempTables — this is a programmer error. ' +
      'In production the SchemaDefaultResolver fills RelatedTableSchema with the platform default for regular templates ' +
      'or {{SchemaName}} for schema templates; a blank value here means a caller bypassed Template.Load.';
    THROW 51000, @v_FkMsg, 1;
  END

  -- Identify ForeignKeys to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #ForeignKeys WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND [KeyName] = ''' + [KeyName] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ForeignKeys WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  RAISERROR('Parse Table Level Check Constraints from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #CheckConstraints
  SELECT t.[Schema], t.[Name] AS [TableName], c.[ConstraintName], c.[Expression], c.[ShouldApplyExpression]
    INTO #CheckConstraints
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(CheckConstraints) WITH (
      [ConstraintName] NVARCHAR(500) '$.Name',
      [Expression] NVARCHAR(MAX) '$.Expression',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression'
      ) c;

  -- Identify CheckConstraints to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG('DELETE FROM #CheckConstraints WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND [ConstraintName] = ''' + [ConstraintName] + ''' AND NOT (' + [ShouldApplyExpression] + ');', CHAR(13) + CHAR(10))
    FROM #CheckConstraints WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)
  
  RAISERROR('Parse Statistics from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Statistics
  SELECT t.[Schema], t.[Name] AS [TableName], [StatisticName] = SchemaSmith.fn_SafeBracketWrap(s.[StatisticName]), [SampleSize] = ISNULL(s.[SampleSize], 0), s.[FilterExpression],
         [Columns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') FROM STRING_SPLIT(s.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         s.[ShouldApplyExpression]
    INTO #Statistics
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON([Statistics]) WITH (
      [StatisticName] NVARCHAR(500) '$.Name',
      [SampleSize] TINYINT '$.SampleSize',
      [FilterExpression] NVARCHAR(MAX) '$.FilterExpression',
      [Columns] NVARCHAR(MAX) '$.Columns',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression'
      ) s;

  -- Identify Statistics to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #Statistics WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND [StatisticName] = ''' + [StatisticName] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Statistics WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)
  
  RAISERROR('Parse Full Text Indexes from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FullTextIndexes
  SELECT t.[Schema], t.[Name] AS [TableName], [FullTextCatalog] = SchemaSmith.fn_SafeBracketWrap(f.[FullTextCatalog]), [KeyIndex] = SchemaSmith.fn_SafeBracketWrap(f.[KeyIndex]), 
         f.[ChangeTracking], [StopList] = SchemaSmith.fn_SafeBracketWrap(COALESCE(NULLIF(RTRIM(f.[StopList]), ''), 'SYSTEM')),
         [Columns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') FROM STRING_SPLIT(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         f.[ShouldApplyExpression]
    INTO #FullTextIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON([FullTextIndex]) WITH (
      [Columns] NVARCHAR(MAX) '$.Columns',
      [FullTextCatalog] NVARCHAR(500) '$.FullTextCatalog',
      [KeyIndex] NVARCHAR(500) '$.KeyIndex',
      [ChangeTracking] NVARCHAR(500) '$.ChangeTracking',
      [StopList] NVARCHAR(500) '$.StopList',
      [ShouldApplyExpression] NVARCHAR(MAX) '$.ShouldApplyExpression'
      ) f;

  -- Identify FullTextIndexes to skip based on ShouldApply expression
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #FullTextIndexes WHERE [Schema] = ''' + [Schema] + ''' AND [TableName] = ''' + [TableName] + ''' AND NOT (' + [ShouldApplyExpression] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #FullTextIndexes WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)
