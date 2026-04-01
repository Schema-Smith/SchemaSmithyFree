-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR ALTER PROCEDURE SchemaSmith.IndexOnlyQuench
  @ProductName NVARCHAR(50),
  @TableDefinitions NVARCHAR(MAX),
  @WhatIf BIT = 0,
  @DropUnknownIndexes BIT = 0,
  @UpdateFillFactor BIT = 1
AS
BEGIN TRY  
  DECLARE @v_SQL NVARCHAR(MAX) = ''
  SET NOCOUNT ON
  RAISERROR('Parse Tables from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #TableDefinitions
  SELECT [Schema] = SchemaSmith.fn_SafeBracketWrap(ISNULL([Schema], 'dbo')), [Name] = SchemaSmith.fn_SafeBracketWrap([Name]), [CompressionType] = ISNULL([CompressionType], 'NONE'),
         [UpdateFillFactor] = ISNULL([UpdateFillFactor], 0), [Indexes], [XmlIndexes], [Statistics], [FullTextIndex]
    INTO #TableDefinitions
    FROM OPENJSON(@TableDefinitions) WITH (
      [Schema] NVARCHAR(500) '$.Schema',
      [Name] NVARCHAR(500) '$.Name',
      [CompressionType] NVARCHAR(100) '$.CompressionType',
      [UpdateFillFactor] BIT '$.UpdateFillFactor',
	  [Indexes] NVARCHAR(MAX) '$.Indexes' AS JSON,
	  [XmlIndexes] NVARCHAR(MAX) '$.XmlIndexes' AS JSON,
	  [Statistics] NVARCHAR(MAX) '$.Statistics' AS JSON,
	  [FullTextIndex] NVARCHAR(MAX) '$.FullTextIndex' AS JSON
      ) t;
  
  DROP TABLE IF EXISTS #Tables
  SELECT [Schema], [Name], [CompressionType], [UpdateFillFactor],
         CONVERT(BIT, CASE WHEN OBJECT_ID([Schema] + '.' + [Name], 'U') IS NULL THEN 1 ELSE 0 END) AS MissingTable
    INTO #Tables
    FROM #TableDefinitions WITH (NOLOCK)

  RAISERROR('Parse Indexes from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Indexes
  SELECT t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), [CompressionType] = ISNULL(i.[CompressionType], 'NONE'), [PrimaryKey] = ISNULL(i.[PrimaryKey], 0), 
         [Unique] = COALESCE(NULLIF(i.[Unique], 0), NULLIF(i.[PrimaryKey], 0), i.[UniqueConstraint], 0),
         [UniqueConstraint] = ISNULL(i.[UniqueConstraint], 0), [Clustered] = ISNULL(i.[Clustered], 0), [ColumnStore] = ISNULL(i.[ColumnStore], 0), [FillFactor] = ISNULL(NULLIF(i.[FillFactor], 0), 100),
         i.[FilterExpression], [UpdateFillFactor] = CONVERT(BIT, CASE WHEN @UpdateFillFactor = 1 OR t.[UpdateFillFactor] = 1 OR i.[UpdateFillFactor] = 1 THEN 1 ELSE 0 END),
         [IndexColumns] = (SELECT STRING_AGG(CAST(CASE WHEN RTRIM([value]) LIKE '% DESC' 
                                                       THEN SchemaSmith.fn_SafeBracketWrap(SUBSTRING(RTRIM([value]), 1, LEN(RTRIM([value])) - 5)) + ' DESC'
                                                       ELSE SchemaSmith.fn_SafeBracketWrap([value])
                                                       END AS NVARCHAR(MAX)), ',') 
                             FROM STRING_SPLIT(i.[IndexColumns], ',') 
                             WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''),
         [IncludeColumns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY SchemaSmith.fn_SafeBracketWrap([value]))
                               FROM STRING_SPLIT(i.[IncludeColumns], ',') 
                               WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '')
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
      [UpdateFillFactor] BIT '$.UpdateFillFactor'
      ) i;
  
  RAISERROR('Parse XML Indexes from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexes
  SELECT t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), i.[IsPrimary],
         [Column] = SchemaSmith.fn_SafeBracketWrap(i.[Column]), [PrimaryIndex] = SchemaSmith.fn_SafeBracketWrap(i.[PrimaryIndex]),
         i.[SecondaryIndexType]
    INTO #XmlIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON(XmlIndexes) WITH (
      [IndexName] NVARCHAR(500) '$.Name',
      [IsPrimary] BIT '$.IsPrimary',
      [Column] NVARCHAR(500) '$.Column',
      [PrimaryIndex] NVARCHAR(500) '$.PrimaryIndex',
	  [SecondaryIndexType] NVARCHAR(500) '$.SecondaryIndexType'
      ) i;
  
  RAISERROR('Parse Statistics from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Statistics
  SELECT t.[Schema], t.[Name] AS [TableName], [StatisticName] = SchemaSmith.fn_SafeBracketWrap(s.[StatisticName]), [SampleSize] = ISNULL(s.[SampleSize], 0), s.[FilterExpression],
         [Columns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') FROM STRING_SPLIT(s.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '')
    INTO #Statistics
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON([Statistics]) WITH (
      [StatisticName] NVARCHAR(500) '$.Name',
      [SampleSize] TINYINT '$.SampleSize',
      [FilterExpression] NVARCHAR(MAX) '$.FilterExpression',
      [Columns] NVARCHAR(MAX) '$.Columns'
      ) s;
  
  RAISERROR('Parse Full Text Indexes from Json', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FullTextIndexes
  SELECT t.[Schema], t.[Name] AS [TableName], [FullTextCatalog] = SchemaSmith.fn_SafeBracketWrap(f.[FullTextCatalog]), [KeyIndex] = SchemaSmith.fn_SafeBracketWrap(f.[KeyIndex]), 
         f.[ChangeTracking], [StopList] = SchemaSmith.fn_SafeBracketWrap(COALESCE(NULLIF(RTRIM(f.[StopList]), ''), 'SYSTEM')),
         [Columns] = (SELECT STRING_AGG(CAST(SchemaSmith.fn_SafeBracketWrap([value]) AS NVARCHAR(MAX)), ',') FROM STRING_SPLIT(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '')
    INTO #FullTextIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY OPENJSON([FullTextIndex]) WITH (
      [Columns] NVARCHAR(MAX) '$.Columns',
      [FullTextCatalog] NVARCHAR(500) '$.FullTextCatalog',
      [KeyIndex] NVARCHAR(500) '$.KeyIndex',
      [ChangeTracking] NVARCHAR(500) '$.ChangeTracking',
      [StopList] NVARCHAR(500) '$.StopList'
      ) f;
  
  -- Handle index compression changes
  RAISERROR('Fixup Index Compression', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Altering index compression for ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ' TO ' + i.[CompressionType] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' REBUILD PARTITION=ALL WITH (DATA_COMPRESSION=' + i.[CompressionType] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Indexes i WITH (NOLOCK) 
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName])
                                     AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    LEFT JOIN sys.partitions p WITH (NOLOCK) ON p.[object_id] = si.[object_id]
                                            AND p.index_id = si.index_id
    WHERE COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') <> i.[CompressionType]
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect Existing Index Definitions', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingIndexes
  SELECT xSchema = t.[Schema], [xTableName] = t.[Name], [xIndexName] = CAST(si.[Name] AS NVARCHAR(500)),
         IsConstraint = CAST(CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1 THEN 1 ELSE 0 END AS BIT),
         IsUnique = si.is_unique, IsClustered = CAST(CASE WHEN si.[type_desc] = 'CLUSTERED' THEN 1 ELSE 0 END AS BIT), [FillFactor] = ISNULL(NULLIF(si.fill_factor, 0), 100),
         IndexScript = 'CREATE ' + 
                       CASE WHEN si.is_unique = 1 THEN 'UNIQUE ' ELSE '' END + 
                       CASE WHEN si.[type] IN (1, 5) THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                       CASE WHEN si.[type] IN (5, 6) THEN 'COLUMNSTORE ' ELSE '' END +
                       'INDEX [' + si.[Name] + '] ON ' + t.[Schema] + '.' + t.[Name] + 
                       CASE WHEN si.[type] NOT IN (5, 6) 
                            THEN ' (' + (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY key_ordinal)
                                           FROM sys.index_columns ic WITH (NOLOCK)
                                           WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 0) + ')' +
                                 CASE WHEN EXISTS (SELECT * FROM sys.index_columns ic WITH (NOLOCK) WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1)
                                      THEN ' INCLUDE (' +
                                           (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(ic.[object_id], ic.column_id))
                                              FROM sys.index_columns ic WITH (NOLOCK)
                                              WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) + ')'
                                      ELSE '' END
                            WHEN si.[type] IN (6) 
                            THEN ' (' + (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(ic.[object_id], ic.column_id))
                                           FROM sys.index_columns ic WITH (NOLOCK)
                                           WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) + ')'
                            ELSE '' END +
                       CASE WHEN si.has_filter = 1 THEN ' WHERE ' + SchemaSmith.fn_StripParenWrapping(si.filter_definition) ELSE '' END +
                       CASE WHEN (si.[type] NOT IN (5, 6) AND ISNULL(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT IN ('NONE', 'ROW', 'PAGE'))
                              OR (si.[type] IN (5, 6) AND ISNULL(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                            THEN ' WITH (DATA_COMPRESSION=' + ISNULL(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT + ')'
                            ELSE '' END
    INTO #ExistingIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                     AND si.index_id > 0
                                     AND is_hypothetical = 0
                                     AND is_disabled = 0
    LEFT JOIN sys.partitions p WITH (NOLOCK)  ON p.[object_id] = si.[object_id]
                                             AND p.index_id = si.index_id
    WHERE t.MissingTable = 0
      AND NOT EXISTS (SELECT * FROM sys.xml_indexes xi WHERE xi.[object_id] = si.[object_id] AND xi.index_id = si.index_id)

  RAISERROR('Collect Existing XML Index Definitions', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingXmlIndexes
  SELECT xSchema = t.[Schema], [xTableName] = t.[Name], [xIndexName] = CAST(i.[Name] COLLATE DATABASE_DEFAULT AS NVARCHAR(500)),
         IndexScript = 'CREATE ' + CASE WHEN i.xml_index_type = 0 THEN 'PRIMARY ' ELSE '' END + 
                       'XML INDEX [' + i.[name] COLLATE DATABASE_DEFAULT + '] ON [' + OBJECT_SCHEMA_NAME(i.[object_id]) + '].[' + OBJECT_NAME(i.[object_id]) + '] ' + 
                       '([' + COL_NAME(i.[Object_id], ic.column_id) + '])' + 
                       CASE WHEN i.xml_index_type = 1 
                            THEN ' USING XML INDEX [' + (SELECT [Name] FROM sys.xml_indexes i2 WHERE i2.[object_id] = i.[object_id] AND i2.index_id = i.using_xml_index_id) COLLATE DATABASE_DEFAULT + '] ' + 
                                 'FOR ' + i.secondary_type_desc COLLATE DATABASE_DEFAULT 
                            ELSE '' END
    INTO #ExistingXmlIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.xml_indexes i ON i.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    JOIN sys.index_columns ic ON i.[object_id] = ic.[object_id] AND i.index_id = ic.index_id
    WHERE t.MissingTable = 0

  RAISERROR('Detect Xml Index Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexChanges
  SELECT i.[Schema], i.[TableName], i.[IndexName]
    INTO #XmlIndexChanges
    FROM #ExistingXmlIndexes ei WITH (NOLOCK)
    JOIN #XmlIndexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                    AND ei.[xTableName] = i.[TableName]
                                    AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE EXISTS (SELECT * 
                    FROM sys.xml_indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND ei.IndexScript <> 'CREATE ' + CASE WHEN i.IsPrimary = 1 THEN 'PRIMARY ' ELSE '' END + 
                            'XML INDEX ' + i.[IndexName] COLLATE DATABASE_DEFAULT + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[Column] + ')' + 
                            CASE WHEN i.IsPrimary = 0
                                 THEN ' USING XML INDEX ' + i.PrimaryIndex + ' FOR ' + i.SecondaryIndexType
                                 ELSE '' END
  
  RAISERROR('Detect Xml Index Renames', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexRenames
  SELECT i.[Schema], i.[TableName], [NewName] = i.[IndexName], [OldName] = ei.[xIndexName]
    INTO #XmlIndexRenames
    FROM #ExistingXmlIndexes ei WITH (NOLOCK)
    JOIN #XmlIndexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                    AND ei.[xTableName] = i.[TableName]
                                    AND ei.[xIndexName] <> SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE NOT EXISTS (SELECT * FROM #XmlIndexes i2 WITH (NOLOCK) WHERE i2.[Schema] = ei.[xSchema] AND i2.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i2.[IndexName]) = ei.[xIndexName])
      AND INDEXPROPERTY(OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]), SchemaSmith.fn_StripBracketWrapping(i.[IndexName]), 'IndexID') IS NULL
      AND EXISTS (SELECT * 
                    FROM sys.xml_indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND REPLACE(ei.IndexScript, ei.[xIndexName], 'IndexName') = 'CREATE ' + CASE WHEN i.IsPrimary = 1 THEN 'PRIMARY ' ELSE '' END + 
                                                                  'XML INDEX ' + i.[IndexName] COLLATE DATABASE_DEFAULT + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[Column] + ')' + 
                                                                  CASE WHEN i.IsPrimary = 0
                                                                       THEN ' USING XML INDEX ' + i.PrimaryIndex + ' FOR ' + i.SecondaryIndexType
                                                                       ELSE '' END

  RAISERROR('Handle Renamed Xml Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Renaming ' + [OldName] + ' to ' + [NewName] + ' ON ' + ir.[Schema] + '.' + ir.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN INDEXPROPERTY(OBJECT_ID(ir.[Schema] + '.' + ir.[TableName]), SchemaSmith.fn_StripBracketWrapping(ir.[NewName]), 'IndexID') IS NULL
                                       THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(ir.[TableName]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''INDEX'';'
                                       ELSE 'DROP INDEX IF EXISTS [' + ir.[OldName] + '] ON ' + ir.[Schema] + '.' + ir.[TableName] + ';'
                                       END AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #XmlIndexRenames ir WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
    
  RAISERROR('Collect Existing FullText Indexes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingFullTextIndexes
  SELECT t.[Schema], [TableName] = t.[Name],
         (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[object_id], fc.column_id) + ']' +
                            CASE WHEN fc.type_column_id IS NOT NULL
                                 THEN ' TYPE COLUMN [' + COL_NAME(fc.[object_id], fc.type_column_id) + ']'
                                 ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(fc.[object_id], fc.column_id))
            FROM sys.fulltext_index_columns fc WITH (NOLOCK)
            WHERE fi.[object_id] = fc.[object_id]) AS [Columns],
         FullTextCatalog = '[' + (SELECT c.[name] COLLATE DATABASE_DEFAULT FROM sys.fulltext_catalogs c WITH (NOLOCK) WHERE c.fulltext_catalog_id = fi.fulltext_catalog_id) + ']',
         KeyIndex = '[' + (SELECT i.[Name] COLLATE DATABASE_DEFAULT FROM sys.indexes i WITH (NOLOCK) WHERE i.[object_id] = fi.[object_id] AND i.[index_id] = fi.[unique_index_id]) + ']',
         ChangeTracking = change_tracking_state_desc COLLATE DATABASE_DEFAULT,
         [StopList] = '[' + COALESCE((SELECT fs.[name] COLLATE DATABASE_DEFAULT FROM sys.fulltext_stoplists fs WITH (NOLOCK) WHERE fs.stoplist_id = fi.stoplist_id), 'SYSTEM') + ']'
    INTO #ExistingFullTextIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    WHERE t.MissingTable = 0
  
  RAISERROR('Detect Index Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexChanges
  SELECT i.[Schema], i.[TableName], i.[IndexName], ei.[IsConstraint], IsUnique = i.[Unique], IsClustered = i.[Clustered]
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
                            CASE WHEN i.[ColumnStore] = 1 THEN 'COLUMNSTORE ' ELSE '' END + 
	                        'INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + 
                            CASE WHEN i.[ColumnStore] = 0 THEN ' (' + i.[IndexColumns] + ')' + CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END
                                 WHEN i.[ColumnStore] = 1 AND i.[Clustered] = 0 THEN ' (' + i.[IncludeColumns] + ')'
                                 ELSE '' END +
                            CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
                            CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                   OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                 THEN ' WITH (DATA_COMPRESSION=' + RTRIM(ISNULL(i.[CompressionType], '')) + ')'
                                 ELSE '' END
  
  RAISERROR('Detect Index Renames', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexRenames
  SELECT i.[Schema], i.[TableName], [NewName] = i.[IndexName], ei.[IsConstraint], IsUnique = i.[Unique], [OldName] = ei.[xIndexName]
    INTO #IndexRenames
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] <> SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE NOT EXISTS (SELECT * FROM #Indexes i2 WITH (NOLOCK) WHERE i2.[Schema] = ei.[xSchema] AND i2.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i2.[IndexName]) = ei.[xIndexName])
      AND INDEXPROPERTY(OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]), SchemaSmith.fn_StripBracketWrapping(i.[IndexName]), 'IndexID') IS NULL
      AND EXISTS (SELECT * 
                    FROM sys.indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND REPLACE(ei.IndexScript, ei.[xIndexName], 'IndexName') = 'CREATE ' + 
                                                                  CASE WHEN i.[Unique] = 1 OR i.[PrimaryKey] = 1 THEN 'UNIQUE ' ELSE '' END + 
                                                                  CASE WHEN i.[Clustered] = 1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                                                                  CASE WHEN i.[ColumnStore] = 1 THEN 'COLUMNSTORE ' ELSE '' END + 
	                                                              'INDEX [IndexName] ON ' + i.[Schema] + '.' + i.[TableName] + 
                                                                  CASE WHEN i.[ColumnStore] = 0 THEN ' (' + i.[IndexColumns] + ')' + CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END
                                                                       WHEN i.[ColumnStore] = 1 AND i.[Clustered] = 0 THEN ' (' + i.[IncludeColumns] + ')'
                                                                       ELSE '' END +
                                                                  CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
                                                                  CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                                                         OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                                                       THEN ' WITH (DATA_COMPRESSION=' + RTRIM(ISNULL(i.[CompressionType], '')) + ')'
                                                                       ELSE '' END

  -- Remove duplicates from the rename list
  SELECT MAX([NewName]) AS ValidNewName, [OldName] AS [OriginalName]
    INTO #IndexRenameDedupe
    FROM #IndexRenames ir WITH (NOLOCK)
    GROUP BY [OldName]  
  DELETE FROM #IndexRenames WHERE EXISTS (SELECT * FROM #IndexRenameDedupe dd WITH (NOLOCK) WHERE [OriginalName] = [OldName] AND [ValidNewName] <> [NewName])
  
  RAISERROR('Handle Renamed Indexes And Unique Constraints', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Renaming ' + [OldName] + ' to ' + [NewName] + ' ON ' + ir.[Schema] + '.' + ir.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN IsConstraint = 1
                                       THEN CASE WHEN OBJECT_ID(ir.[Schema] + '.' + ir.[NewName]) IS NULL
                                                 THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''OBJECT'';'
                                                 ELSE 'ALTER TABLE ' + ir.[Schema] + '.' + ir.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + ir.[OldName] + '];'
                                                 END
                                       ELSE CASE WHEN INDEXPROPERTY(OBJECT_ID(ir.[Schema] + '.' + ir.[TableName]), SchemaSmith.fn_StripBracketWrapping(ir.[NewName]), 'IndexID') IS NULL
                                                 THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(ir.[TableName]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''INDEX'';'
                                                 ELSE 'DROP INDEX IF EXISTS [' + ir.[OldName] + '] ON ' + ir.[Schema] + '.' + ir.[TableName] + ';'
                                                 END
                                       END AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #IndexRenames ir WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect index level extended properties', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexProperties
  SELECT t.[Schema], t.[Name] AS TableName, objname COLLATE DATABASE_DEFAULT AS IndexName, x.[Name] COLLATE DATABASE_DEFAULT AS PropertyName, CONVERT(NVARCHAR(50), x.[value]) COLLATE DATABASE_DEFAULT AS [value]
    INTO #IndexProperties
    FROM #Tables t WITH (NOLOCK)
    CROSS APPLY fn_listextendedproperty(default, 'Schema', SchemaSmith.fn_StripBracketWrapping(t.[Schema]), 'Table', SchemaSmith.fn_StripBracketWrapping(t.[Name]), 'Index', default) x
  WHERE x.[Name] COLLATE DATABASE_DEFAULT = 'ProductName'

  RAISERROR('Identify indexes removed from the product', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesRemovedFromProduct
  SELECT xp.[Schema], xp.TableName, xp.IndexName, IsConstraint = CAST(CASE WHEN OBJECT_ID(xp.[Schema] + '.' + xp.IndexName) IS NOT NULL THEN 1 ELSE 0 END AS BIT)
    INTO #IndexesRemovedFromProduct
    FROM #IndexProperties xp
    WHERE xp.[value] = @ProductName
      AND NOT EXISTS (SELECT *
                        FROM #Indexes i WITH (NOLOCK)
                        WHERE i.[Schema] = xp.[Schema]
                          AND i.TableName = xp.TableName
                          AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = xp.IndexName)
      AND NOT EXISTS (SELECT *
                        FROM #XmlIndexes i WITH (NOLOCK)
                        WHERE i.[Schema] = xp.[Schema]
                          AND i.TableName = xp.TableName
                          AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = xp.IndexName)
                      
  RAISERROR('Identify unknown and modified indexes to drop', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesToDrop
  -- Indexes removed from the prouduct
  SELECT [Schema] = CAST([Schema] AS NVARCHAR(500)), [TableName] = CAST([TableName] AS NVARCHAR(500)),
         [IndexName] = CAST(SchemaSmith.fn_StripBracketWrapping([IndexName]) AS NVARCHAR(500)), [IsConstraint], [IsUnique] = i.[is_unique],
         [IsClustered] = CAST(CASE WHEN i.[type_desc] = 'CLUSTERED' THEN 1 ELSE 0 END AS BIT)
    INTO #IndexesToDrop
    FROM #IndexesRemovedFromProduct ir WITH (NOLOCK)
    JOIN sys.indexes i WITH (NOLOCK) ON i.[object_id] = OBJECT_ID([Schema] + '.' + [TableName]) AND i.[Name] = SchemaSmith.fn_StripBracketWrapping([IndexName])
  UNION
  -- Unknown indexes if we're dropping unknown
  SELECT [Schema] = CAST([xSchema] AS NVARCHAR(500)), [TableName] = CAST([xTableName] AS NVARCHAR(500)), [IndexName] = CAST([xIndexName] AS NVARCHAR(500)), IsConstraint, IsUnique, IsClustered
    FROM #ExistingIndexes di WITH (NOLOCK)
    WHERE @DropUnknownIndexes = 1
      AND NOT EXISTS (SELECT * FROM #Indexes i WITH (NOLOCK) WHERE i.[Schema] = di.[xSchema] AND i.[TableName] = di.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i.[IndexName]) = di.[xIndexName])
  UNION 
  -- Indexes where the index definition was modified
  SELECT i.[Schema], i.[TableName], SchemaSmith.fn_StripBracketWrapping(i.[IndexName]), i.[IsConstraint], i.[IsUnique], i.[IsClustered]
    FROM #IndexChanges i WITH (NOLOCK)
  UNION
  -- Unknown xml indexes if we're dropping unknown
  SELECT [xSchema], [xTableName], [xIndexName], [IsConstraint] = 0, [IsUnique] = 0, [IsClustered] = 0
    FROM #ExistingXmlIndexes ei WITH (NOLOCK)
    WHERE @DropUnknownIndexes = 1
      AND NOT EXISTS (SELECT * FROM #XmlIndexes i WITH (NOLOCK) WHERE i.[Schema] = ei.[xSchema] AND i.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i.[IndexName]) = ei.[xIndexName])
  UNION
  -- Xml Indexes where the index definition was modified
  SELECT [Schema], [TableName], SchemaSmith.fn_StripBracketWrapping([IndexName]), [IsConstraint] = 0, [IsUnique] = 0, [IsClustered] = 0
    FROM #XmlIndexChanges WITH (NOLOCK)

  -- Need to drop all the XML indexes if we're removing the clustered PK
  INSERT #IndexesToDrop ([Schema], [TableName], [IndexName], [IsConstraint], [IsUnique], [IsClustered])
    SELECT [xSchema], [xTableName], [xIndexName], [IsConstraint] = 0, [IsUnique] = 0, [IsClustered] = 0
      FROM #ExistingXmlIndexes ei WITH (NOLOCK)
      WHERE EXISTS (SELECT * FROM #IndexesToDrop id WITH (NOLOCK) WHERE [xSchema] = [Schema] AND [xTableName] = [TableName] AND id.[IsClustered] = 1)
        AND NOT EXISTS (SELECT * FROM #IndexesToDrop id WITH (NOLOCK) WHERE [xSchema] = [Schema] AND [xTableName] = [TableName] AND [xIndexName] = [IndexName])

  RAISERROR('Drop Referencing Foreign Keys When Dropping Unique Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping foreign Key ' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) + '.' + fk.[name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE [' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + fk.[name] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #IndexesToDrop di WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.referenced_object_id = OBJECT_ID(di.[Schema] + '.' + di.[TableName])
    WHERE IsConstraint = 1 OR IsUnique = 1
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop FullText Indexes Referencing Unique Indexes That Will Be Dropped', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping fulltext index on ' + ef.[Schema] + '.' + ef.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP FULLTEXT INDEX ON ' + ef.[Schema] + '.' + ef.[TableName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #IndexesToDrop id WITH (NOLOCK)
    JOIN #ExistingFullTextIndexes ef WITH (NOLOCK) ON id.[Schema] = ef.[Schema]
                                                  AND id.[TableName] = ef.[TableName]
                                                  AND id.[IndexName] = SchemaSmith.fn_StripBracketWrapping(ef.[KeyIndex])
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(ef.[Schema] + '.' + ef.[TableName])
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Unknown and Modified Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping ' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + di.[Schema] + '.' + di.[TableName] + '.' + di.[IndexName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN IsConstraint = 1
                                       THEN 'ALTER TABLE ' + di.[Schema] + '.' + di.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + di.[IndexName] + '];'
                                       ELSE 'DROP INDEX IF EXISTS [' + di.[IndexName] + '] ON ' + di.[Schema] + '.' + di.[TableName] + ';'
                                       END AS NVARCHAR(MAX)), CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY CASE WHEN [IsClustered] = 0 THEN 0 ELSE 1 END)
    FROM #IndexesToDrop di WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Fixup Modified Fillfactors', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Fixup ' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ' fillfactor in ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ''', 10, 100) WITH NOWAIT; ' + 
                                  'ALTER INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' REBUILD WITH (FILLFACTOR = ' + CONVERT(NVARCHAR(5), i.[FillFactor]) + ', SORT_IN_TEMPDB = ON);' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE i.[UpdateFillFactor] = 1
      AND ei.[FillFactor] <> i.[FillFactor]
      AND INDEXPROPERTY(OBJECT_ID(i.[Schema] + '.' + i.[TableName]), ei.[xIndexName], 'IndexID') IS NOT NULL
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing Statistics Definitions', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingStats
  SELECT t.[Schema], [TableName] = t.[Name], [StatsName] = si.[Name],
         StatisticScript = 'CREATE STATISTICS ' +
                           '[' + si.[Name] + '] ON ' + t.[Schema] + '.' + t.[Name] + ' (' +
                           (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY ic.stats_column_id)
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
    WHERE t.MissingTable = 0
  
  RAISERROR('Detect Statistics Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #StatsChanges
  SELECT s.[Schema], s.[TableName], s.[StatisticName]
    INTO #StatsChanges
    FROM #Statistics s WITH (NOLOCK)
    JOIN #ExistingStats es WITH (NOLOCK) ON s.[Schema] = es.[Schema]
                                        AND s.[TableName] = es.[TableName]
                                        AND SchemaSmith.fn_StripBracketWrapping(s.[StatisticName]) = es.[StatsName]
    WHERE es.StatisticScript <> 'CREATE STATISTICS [' + s.[StatisticName] + '] ON ' + s.[Schema] + '.' + s.[TableName] + ' (' + s.[Columns] + ')' +
                                CASE WHEN RTRIM(ISNULL(s.[FilterExpression], '')) <> '' THEN ' WHERE ' + s.[FilterExpression] ELSE '' END
  
  RAISERROR('Drop Modified Statistics', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping statistics ' + sc.[Schema] + '.' + sc.[TableName] + '.' + sc.[StatisticName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP STATISTICS ' + sc.[Schema] + '.' + sc.[TableName] + '.' + sc.[StatisticName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #StatsChanges sc WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Add Missing Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Creating ' + CASE WHEN i.PrimaryKey = 1 OR i.UniqueConstraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN i.PrimaryKey = 1 OR i.UniqueConstraint = 1
                                       THEN 'ALTER TABLE ' + i.[Schema] + '.' + i.[TableName] + ' ADD CONSTRAINT ' + i.[IndexName] +
                                            CASE WHEN i.PrimaryKey = 1 THEN ' PRIMARY KEY ' WHEN i.UniqueConstraint = 1 THEN ' UNIQUE ' END +
                                            CASE WHEN i.[Clustered] =  1 THEN '' ELSE 'NON' END + 'CLUSTERED (' + i.IndexColumns + ')' +
					                        CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE')
                                                   OR ISNULL(i.[FillFactor], 100) NOT IN (0, 100)
                                                 THEN ' WITH (' +
                                                      CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE') THEN 'DATA_COMPRESSION=' + i.[CompressionType] ELSE '' END +
                                                      CASE WHEN ISNULL(i.[FillFactor], 100) NOT IN (0, 100) 
                                                           THEN CASE WHEN RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE') THEN ', ' ELSE '' END +
                                                                'FILLFACTOR = ' + CAST(i.[FillFactor] AS NVARCHAR(20)) 
                                                           ELSE '' END +
							                          ')'
                                                 ELSE '' END
                                       ELSE 'CREATE ' + 
                                            CASE WHEN i.[Unique] = 1 THEN 'UNIQUE ' ELSE '' END +
                                            CASE WHEN i.[Clustered] =  1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                                            CASE WHEN i.[ColumnStore] = 1 THEN 'COLUMNSTORE ' ELSE '' END +
                                            'INDEX ' + i.[IndexName] +
                                            ' ON ' + i.[Schema] + '.' + i.[TableName] + 
                                            CASE WHEN i.[ColumnStore] = 0 THEN ' (' + i.[IndexColumns] + ')' + CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END
                                                 WHEN i.[ColumnStore] = 1 AND i.[Clustered] = 0 THEN ' (' + i.[IncludeColumns] + ')'
                                                 ELSE '' END +
                                            CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
					                        CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                                   OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                                   OR ISNULL(i.[FillFactor], 100) NOT IN (0, 100)
                                                 THEN ' WITH (' +
                                                      CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                                             OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                                           THEN 'DATA_COMPRESSION=' + i.[CompressionType] ELSE '' END +
                                                      CASE WHEN ISNULL(i.[FillFactor], 100) NOT IN (0, 100) 
                                                           THEN CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                                                       OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                                                     THEN ', ' ELSE '' END +
                                                                'FILLFACTOR = ' + CAST(i.[FillFactor] AS NVARCHAR(20)) 
                                                           ELSE '' END +
							                          ')'
                                                 ELSE '' END
                                       END + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY i.[Schema], i.[TableName], CASE WHEN i.[Clustered] =  1 THEN 0 ELSE 1 END, i.[IndexName])
    FROM #Indexes i WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * 
                        FROM sys.indexes si WITH (NOLOCK)
                        WHERE si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName]) 
                          AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName]))    
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add missing ProductName extended property to indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('EXEC sp_addextendedproperty @name = N''ProductName'', @value = ''' + @ProductName + ''', ' +
                                  '@level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', ' +
                                  '@level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', ' +
                                  '@level2type = N''Index'', @level2name = ''' + SchemaSmith.fn_StripBracketWrapping(i.IndexName) + ''';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Indexes i WITH (NOLOCK)
    JOIN #Tables t WITH (NOLOCK) ON t.[Schema] = i.[Schema] AND t.[Name] = i.[TableName]
    WHERE INDEXPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name]), SchemaSmith.fn_StripBracketWrapping(i.IndexName), 'IndexID') IS NOT NULL
      AND NOT EXISTS (SELECT * FROM #IndexProperties ip WITH (NOLOCK) WHERE i.[Schema] = ip.[Schema] AND i.TableName = ip.TableName AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = ip.IndexName AND ip.PropertyName = 'ProductName')
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add Missing Xml Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Creating index ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'CREATE ' + CASE WHEN i.IsPrimary = 1 THEN 'PRIMARY ' ELSE '' END + 
                                  'XML INDEX ' + i.[IndexName] COLLATE DATABASE_DEFAULT + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[Column] + ')' +
                                  CASE WHEN i.IsPrimary = 0
                                       THEN ' USING XML INDEX ' + i.PrimaryIndex + ' FOR ' + i.SecondaryIndexType
                                       ELSE '' END + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY i.[Schema], i.[TableName], CASE WHEN i.IsPrimary =  1 THEN 0 ELSE 1 END, i.[IndexName])
    FROM #XmlIndexes i WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * 
                        FROM sys.xml_indexes si WITH (NOLOCK)
                        WHERE si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName]) 
                          AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName]))    
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add missing ProductName extended property to xml indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('EXEC sp_addextendedproperty @name = N''ProductName'', @value = ''' + @ProductName + ''', ' +
                                  '@level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', ' +
                                  '@level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', ' +
                                  '@level2type = N''Index'', @level2name = ''' + SchemaSmith.fn_StripBracketWrapping(i.IndexName) + ''';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #XmlIndexes i WITH (NOLOCK)
    JOIN #Tables t WITH (NOLOCK) ON t.[Schema] = i.[Schema] AND t.[Name] = i.[TableName]
    WHERE INDEXPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name]), SchemaSmith.fn_StripBracketWrapping(i.IndexName), 'IndexID') IS NOT NULL
      AND NOT EXISTS (SELECT * FROM #IndexProperties ip WITH (NOLOCK) WHERE i.[Schema] = ip.[Schema] AND i.TableName = ip.TableName AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = ip.IndexName AND ip.PropertyName = 'ProductName')
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add Missing Statistics', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Creating statistics ' + s.[Schema] + '.' + s.[TableName] + '.' + s.[StatisticName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'CREATE STATISTICS ' + s.[StatisticName] + ' ON ' + s.[Schema] + '.' + s.[TableName] + ' (' + s.[Columns] + ')' +
                                  CASE WHEN RTRIM(ISNULL(s.[FilterExpression], '')) <> '' THEN ' WHERE ' + s.[FilterExpression] ELSE '' END +
                                  ' WITH SAMPLE ' + CAST(ISNULL(s.[SampleSize], 100) AS NVARCHAR(20)) + ' PERCENT;' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Statistics s WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * 
                        FROM sys.stats ss WITH (NOLOCK)
                        WHERE ss.[object_id] = OBJECT_ID(s.[Schema] + '.' + s.[TableName]) 
                          AND ss.[name] = SchemaSmith.fn_StripBracketWrapping(s.[StatisticName]))
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Drop Modified or Removed FullText Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping fulltext index on ' + ei.[Schema] + '.' + ei.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP FULLTEXT INDEX ON ' + ei.[Schema] + '.' + ei.[TableName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
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
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add Missing FullText Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding fulltext index on ' + fi.[Schema] + '.' + fi.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'CREATE FULLTEXT INDEX ON ' + fi.[Schema] + '.' + fi.[TableName] + ' (' + [Columns] + ') KEY INDEX ' + [KeyIndex] + ' ON ' + [FullTextCatalog] + 
                                  ' WITH CHANGE_TRACKING = ' + [ChangeTracking] +
                                  CASE WHEN RTRIM(ISNULL(fi.[StopList], '')) <> '' THEN ', STOPLIST = ' + [StopList] ELSE '' END + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #FullTextIndexes fi WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * FROM sys.fulltext_indexes ft WITH (NOLOCK) WHERE ft.[object_id] = OBJECT_ID(fi.[Schema] + '.' + fi.[TableName]))
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  SET NOCOUNT OFF
END TRY
BEGIN CATCH
  THROW
END CATCH