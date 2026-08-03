-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- XML ingest twin of ParseTableJsonIntoTempTables.sql, selected below the OPENJSON compat cliff
-- (compatibility level < 130) or via Target:CompatEncoding=legacy. Reads an ambient @TableDefinitions XML
-- variable (declared by the caller). Each block shreds with .nodes()/.value() into a derived projection
-- that reproduces the exact column set the JSON script's OPENJSON WITH produced, so the surrounding
-- projection/gate logic is identical to the JSON twin and the 8 consumed temp tables come out identical.
-- Booleans arrive as 'true'/'false' text (see ModelXmlSerializer); a direct BIT cast of 'true' errors, so
-- each is CASEd. Each <Table> subtree is carried in [TableXml] for the nested CROSS APPLY .nodes().

  DECLARE @v_SQL NVARCHAR(MAX) = ''
  SET NOCOUNT ON
  RAISERROR('Parse Tables from Xml', 10, 100) WITH NOWAIT

  -- Accept @TableDefinitions whether it is XML-typed (standalone callers declare @TableDefinitions XML) or
  -- NVARCHAR(MAX) (when inlined via {{ParseJson}} into TableQuench, whose param is NVARCHAR(MAX) — .nodes()
  -- cannot bind to an nvarchar). CONVERT(XML, <xml>) is a no-op; CONVERT(XML, <nvarchar>) parses the text.
  DECLARE @v_TableXml XML = CONVERT(XML, @TableDefinitions)

  -- I5: missing/blank [Schema] is a programmer error after slice-1's SchemaDefaultResolver (see JSON twin).
  IF EXISTS (SELECT 1 FROM @v_TableXml.nodes('/Tables/Table') AS X(t)
                 WHERE NULLIF(RTRIM(ISNULL(t.value('(Schema/text())[1]', 'NVARCHAR(500)'), '')), '') IS NULL)
  BEGIN
    DECLARE @v_BadTable NVARCHAR(500) =
      (SELECT TOP 1 ISNULL(t.value('(Name/text())[1]', 'NVARCHAR(500)'), '<unnamed>') FROM @v_TableXml.nodes('/Tables/Table') AS X(t)
         WHERE NULLIF(RTRIM(ISNULL(t.value('(Schema/text())[1]', 'NVARCHAR(500)'), '')), '') IS NULL);
    DECLARE @v_Msg NVARCHAR(2000) = 'Table XML is missing Schema for table ''' + @v_BadTable + '''. ' +
      'Schema must be populated before reaching ParseTableXmlIntoTempTables — this is a programmer error. ' +
      'In production the SchemaDefaultResolver fills Schema with the platform default or the {{SchemaName}} token; ' +
      'a blank value here means a caller bypassed Template.Load or substituted the token away.';
    RAISERROR(@v_Msg, 16, 1);
  END

  DROP TABLE IF EXISTS #TableDefinitions
  -- [_RowId] gives each parsed row a unique identifier for the per-row ShouldApply DELETE (see JSON twin).
  -- [TableXml] carries the whole <Table> element so the nested blocks below re-navigate with .nodes().
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         [Schema] = SchemaSmith.fn_SafeBracketWrap(t.value('(Schema/text())[1]', 'NVARCHAR(500)')), [Name] = SchemaSmith.fn_SafeBracketWrap(t.value('(Name/text())[1]', 'NVARCHAR(500)')), [CompressionType] = ISNULL(NULLIF(RTRIM(t.value('(CompressionType/text())[1]', 'NVARCHAR(100)')), ''), 'NONE'),
         [IsTemporal] = ISNULL(CONVERT(BIT, CASE LOWER(t.value('(IsTemporal/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0), [UpdateFillFactor] = ISNULL(CONVERT(BIT, CASE LOWER(t.value('(UpdateFillFactor/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0),
         [TableXml] = t.query('.'),
         [ShouldApplyExpression] = t.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'), [VariantName] = t.value('(VariantName/text())[1]', 'NVARCHAR(128)'), [EnableCDC] = ISNULL(CONVERT(BIT, CASE LOWER(t.value('(EnableCDC/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0), [OldName] = SchemaSmith.fn_SafeBracketWrap(t.value('(OldName/text())[1]', 'NVARCHAR(500)')),
         [DropColumnsRemovedFromProduct] = CONVERT(BIT, CASE LOWER(t.value('(DropColumnsRemovedFromProduct/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
         [DropForeignKeysRemovedFromProduct] = CONVERT(BIT, CASE LOWER(t.value('(DropForeignKeysRemovedFromProduct/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
         [DropCheckConstraintsRemovedFromProduct] = CONVERT(BIT, CASE LOWER(t.value('(DropCheckConstraintsRemovedFromProduct/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
         [DropExcludeConstraintsRemovedFromProduct] = CONVERT(BIT, CASE LOWER(t.value('(DropExcludeConstraintsRemovedFromProduct/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
         [DropStatisticsRemovedFromProduct] = CONVERT(BIT, CASE LOWER(t.value('(DropStatisticsRemovedFromProduct/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
         [DropIndexesRemovedFromProduct] = CONVERT(BIT, CASE LOWER(t.value('(DropIndexesRemovedFromProduct/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
         [PreventDrop] = ISNULL(CONVERT(BIT, CASE LOWER(t.value('(PreventDrop/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0)
    INTO #TableDefinitions
    FROM @v_TableXml.nodes('/Tables/Table') AS X(t);

  -- Identify Tables to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #TableDefinitions WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #TableDefinitions WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  DROP TABLE IF EXISTS #Tables
  SELECT [Schema], [Name], [CompressionType], [IsTemporal], [UpdateFillFactor], [EnableCDC], [OldName], [VariantName],
         CONVERT(BIT, CASE WHEN OBJECT_ID([Schema] + '.' + [Name], 'U') IS NULL AND OBJECT_ID([Schema] + '.' + [OldName], 'U') IS NULL THEN 1 ELSE 0 END) AS NewTable,
         [DropColumnsRemovedFromProduct], [DropForeignKeysRemovedFromProduct], [DropCheckConstraintsRemovedFromProduct], [DropExcludeConstraintsRemovedFromProduct], [DropStatisticsRemovedFromProduct], [DropIndexesRemovedFromProduct],
         ISNULL([PreventDrop], 0) AS [PreventDrop]
    INTO #Tables
    FROM #TableDefinitions WITH (NOLOCK);

  RAISERROR('Parse Columns from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Columns
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], [ColumnName] = SchemaSmith.fn_SafeBracketWrap(c.[ColumnName]),
         -- Canonicalize DATETIME2/TIME/DATETIMEOFFSET to explicit (7) — see JSON twin for the rationale.
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
                            AND COLUMNPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[OldName], 'U'), SchemaSmith.fn_StripBracketWrapping([ColumnName]), 'ColumnId') IS NULL
                            AND COLUMNPROPERTY(OBJECT_ID(t.[Schema] + '.' + t.[Name], 'U'), SchemaSmith.fn_StripBracketWrapping(c.[OldName]), 'ColumnId') IS NULL
                           THEN 1 ELSE 0 END) AS NewColumn,
         SchemaSmith.fn_SafeBracketWrap(c.[ColumnName]) + ' ' +
         CASE WHEN RTRIM(ISNULL([ComputedExpression], '')) <> '' THEN 'AS (' + ComputedExpression + ')' + CASE WHEN ISNULL(c.[Persisted], 0) = 1 THEN ' PERSISTED' ELSE '' END
                                                                                                     + CASE WHEN ISNULL(c.[Persisted], 0) = 1 AND ISNULL(c.[Nullable], 1) = 0 THEN ' NOT NULL' ELSE '' END
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
         c.[ShouldApplyExpression], c.[VariantName]
    INTO #Columns
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/Columns') AS Y(col)
    CROSS APPLY (SELECT
      [ColumnName] = col.value('(Name/text())[1]', 'NVARCHAR(500)'),
      [DataType] = col.value('(DataType/text())[1]', 'NVARCHAR(100)'),
      [Nullable] = CONVERT(BIT, CASE LOWER(col.value('(Nullable/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [Default] = col.value('(Default/text())[1]', 'NVARCHAR(MAX)'),
      [CheckExpression] = col.value('(CheckExpression/text())[1]', 'NVARCHAR(MAX)'),
      [ComputedExpression] = col.value('(ComputedExpression/text())[1]', 'NVARCHAR(MAX)'),
      [Persisted] = CONVERT(BIT, CASE LOWER(col.value('(Persisted/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [Sparse] = CONVERT(BIT, CASE LOWER(col.value('(Sparse/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [Collation] = col.value('(Collation/text())[1]', 'NVARCHAR(500)'),
      [DataMaskFunction] = col.value('(DataMaskFunction/text())[1]', 'NVARCHAR(500)'),
      [EncryptionType] = col.value('(EncryptionType/text())[1]', 'NVARCHAR(100)'),
      [EncryptionKey] = col.value('(EncryptionKey/text())[1]', 'NVARCHAR(500)'),
      [EncryptionAlgorithm] = col.value('(EncryptionAlgorithm/text())[1]', 'NVARCHAR(500)'),
      [ShouldApplyExpression] = col.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = col.value('(VariantName/text())[1]', 'NVARCHAR(128)'),
      [OldName] = col.value('(OldName/text())[1]', 'NVARCHAR(500)')
      ) c;

  -- Identify Columns to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #Columns WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Columns WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  -- Don't try to apply tables without columns
  DELETE FROM #Tables
    WHERE NOT EXISTS (SELECT * FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = #Tables.[Schema] AND C.[TableName] = #Tables.[Name])
  DELETE FROM #TableDefinitions
    WHERE NOT EXISTS (SELECT * FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = #TableDefinitions.[Schema] AND C.[TableName] = #TableDefinitions.[Name])

  RAISERROR('Parse Indexes from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Indexes
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), [CompressionType] = ISNULL(NULLIF(RTRIM(i.[CompressionType]), ''), 'NONE'), [PrimaryKey] = ISNULL(i.[PrimaryKey], 0),
         [Unique] = COALESCE(NULLIF(i.[Unique], 0), NULLIF(i.[PrimaryKey], 0), i.[UniqueConstraint], 0),
         [UniqueConstraint] = ISNULL(i.[UniqueConstraint], 0), [Clustered] = ISNULL(i.[Clustered], 0), [ColumnStore] = ISNULL(i.[ColumnStore], 0), [FillFactor] = ISNULL(NULLIF(i.[FillFactor], 0), 100),
         i.[FilterExpression], [UpdateFillFactor] = CONVERT(BIT, CASE WHEN @UpdateFillFactor = 1 OR t.[UpdateFillFactor] = 1 OR i.[UpdateFillFactor] = 1 THEN 1 ELSE 0 END),
         [IndexColumns] = STUFF((SELECT ',' + CASE WHEN RTRIM([value]) LIKE '% DESC'
                                                   THEN SchemaSmith.fn_SafeBracketWrap(SUBSTRING(RTRIM([value]), 1, LEN(RTRIM([value])) - 5)) + ' DESC'
                                                   ELSE SchemaSmith.fn_SafeBracketWrap([value])
                                                   END
                             FROM SchemaSmith.fn_SplitList(i.[IndexColumns], ',')
                             WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''
                             ORDER BY [Ordinal] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),
         [IncludeColumns] = STUFF((SELECT ',' + SchemaSmith.fn_SafeBracketWrap([value])
                               FROM SchemaSmith.fn_SplitList(i.[IncludeColumns], ',')
                               WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> ''
                               ORDER BY SchemaSmith.fn_SafeBracketWrap([value]) FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),
         i.[ShouldApplyExpression], i.[VariantName]
    INTO #Indexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/Indexes') AS Y(idx)
    CROSS APPLY (SELECT
      [IndexName] = idx.value('(Name/text())[1]', 'NVARCHAR(500)'),
      [CompressionType] = idx.value('(CompressionType/text())[1]', 'NVARCHAR(100)'),
      [PrimaryKey] = CONVERT(BIT, CASE LOWER(idx.value('(PrimaryKey/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [Unique] = CONVERT(BIT, CASE LOWER(idx.value('(Unique/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [UniqueConstraint] = CONVERT(BIT, CASE LOWER(idx.value('(UniqueConstraint/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [Clustered] = CONVERT(BIT, CASE LOWER(idx.value('(Clustered/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [ColumnStore] = CONVERT(BIT, CASE LOWER(idx.value('(ColumnStore/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [FillFactor] = idx.value('(FillFactor/text())[1]', 'TINYINT'),
      [FilterExpression] = idx.value('(FilterExpression/text())[1]', 'NVARCHAR(MAX)'),
      [IndexColumns] = idx.value('(IndexColumns/text())[1]', 'NVARCHAR(MAX)'),
      [IncludeColumns] = idx.value('(IncludeColumns/text())[1]', 'NVARCHAR(MAX)'),
      [UpdateFillFactor] = CONVERT(BIT, CASE LOWER(idx.value('(UpdateFillFactor/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [ShouldApplyExpression] = idx.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = idx.value('(VariantName/text())[1]', 'NVARCHAR(128)')
      ) i;

  -- Identify Indexes to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #Indexes WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Indexes WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  RAISERROR('Parse XML Indexes from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexes
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], [IndexName] = SchemaSmith.fn_SafeBracketWrap(i.[IndexName]), i.[IsPrimary],
         [Column] = SchemaSmith.fn_SafeBracketWrap(i.[Column]), [PrimaryIndex] = SchemaSmith.fn_SafeBracketWrap(i.[PrimaryIndex]),
         i.[SecondaryIndexType], i.[ShouldApplyExpression], i.[VariantName]
    INTO #XmlIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/XmlIndexes') AS Y(xi)
    CROSS APPLY (SELECT
      [IndexName] = xi.value('(Name/text())[1]', 'NVARCHAR(500)'),
      [IsPrimary] = CONVERT(BIT, CASE LOWER(xi.value('(IsPrimary/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END),
      [Column] = xi.value('(Column/text())[1]', 'NVARCHAR(500)'),
      [PrimaryIndex] = xi.value('(PrimaryIndex/text())[1]', 'NVARCHAR(500)'),
      [SecondaryIndexType] = xi.value('(SecondaryIndexType/text())[1]', 'NVARCHAR(500)'),
      [ShouldApplyExpression] = xi.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = xi.value('(VariantName/text())[1]', 'NVARCHAR(128)')
      ) i;

  -- Identify XmlIndexes to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #XmlIndexes WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #XmlIndexes WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  RAISERROR('Parse Foreign Keys from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ForeignKeys
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], [KeyName] = SchemaSmith.fn_SafeBracketWrap(f.[KeyName]),
         [RelatedTableSchema] = SchemaSmith.fn_SafeBracketWrap(f.[RelatedTableSchema]), [RelatedTable] = SchemaSmith.fn_SafeBracketWrap(f.[RelatedTable]),
         [Columns] = STUFF((SELECT ',' + SchemaSmith.fn_SafeBracketWrap([value]) FROM SchemaSmith.fn_SplitList(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '' ORDER BY [Ordinal] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),
         [RelatedColumns] = STUFF((SELECT ',' + SchemaSmith.fn_SafeBracketWrap([value]) FROM SchemaSmith.fn_SplitList(f.[RelatedColumns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '' ORDER BY [Ordinal] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),
         [DeleteAction] = ISNULL(NULLIF(RTRIM([DeleteAction]), ''), 'NO ACTION'),
         [UpdateAction] = ISNULL(NULLIF(RTRIM([UpdateAction]), ''), 'NO ACTION'),
         f.[ShouldApplyExpression], f.[VariantName]
    INTO #ForeignKeys
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/ForeignKeys') AS Y(fk)
    CROSS APPLY (SELECT
      [KeyName] = fk.value('(Name/text())[1]', 'NVARCHAR(500)'),
      [Columns] = fk.value('(Columns/text())[1]', 'NVARCHAR(MAX)'),
      [RelatedTableSchema] = fk.value('(RelatedTableSchema/text())[1]', 'NVARCHAR(500)'),
      [RelatedTable] = fk.value('(RelatedTable/text())[1]', 'NVARCHAR(500)'),
      [RelatedColumns] = fk.value('(RelatedColumns/text())[1]', 'NVARCHAR(MAX)'),
      [ShouldApplyExpression] = fk.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = fk.value('(VariantName/text())[1]', 'NVARCHAR(128)'),
      [DeleteAction] = fk.value('(DeleteAction/text())[1]', 'NVARCHAR(20)'),
      [UpdateAction] = fk.value('(UpdateAction/text())[1]', 'NVARCHAR(20)')
      ) f;

  -- I5: post-parse RelatedTableSchema check — identical to JSON twin.
  IF EXISTS (SELECT 1 FROM #ForeignKeys WITH (NOLOCK)
               WHERE [RelatedTableSchema] IS NULL OR [RelatedTableSchema] IN ('[]', '[ ]', ''))
  BEGIN
    DECLARE @v_BadFk NVARCHAR(500) =
      (SELECT TOP 1 ISNULL([KeyName], '<unnamed>') FROM #ForeignKeys WITH (NOLOCK)
         WHERE [RelatedTableSchema] IS NULL OR [RelatedTableSchema] IN ('[]', '[ ]', ''));
    DECLARE @v_FkMsg NVARCHAR(2000) = 'Foreign key ''' + @v_BadFk + ''' is missing RelatedTableSchema. ' +
      'RelatedTableSchema must be populated before reaching ParseTableXmlIntoTempTables — this is a programmer error. ' +
      'In production the SchemaDefaultResolver fills RelatedTableSchema with the platform default for regular templates ' +
      'or {{SchemaName}} for schema templates; a blank value here means a caller bypassed Template.Load.';
    RAISERROR(@v_FkMsg, 16, 1);
  END

  -- Identify ForeignKeys to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #ForeignKeys WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ForeignKeys WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  RAISERROR('Parse Table Level Check Constraints from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #CheckConstraints
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], c.[ConstraintName], c.[Expression], c.[ShouldApplyExpression], c.[VariantName]
    INTO #CheckConstraints
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/CheckConstraints') AS Y(cc)
    CROSS APPLY (SELECT
      [ConstraintName] = cc.value('(Name/text())[1]', 'NVARCHAR(500)'),
      [Expression] = cc.value('(Expression/text())[1]', 'NVARCHAR(MAX)'),
      [ShouldApplyExpression] = cc.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = cc.value('(VariantName/text())[1]', 'NVARCHAR(128)')
      ) c;

  -- Identify CheckConstraints to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG('DELETE FROM #CheckConstraints WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');', CHAR(13) + CHAR(10))
    FROM #CheckConstraints WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  RAISERROR('Parse Statistics from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #Statistics
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], [StatisticName] = SchemaSmith.fn_SafeBracketWrap(s.[StatisticName]), [SampleSize] = ISNULL(s.[SampleSize], 0), s.[FilterExpression],
         [Columns] = STUFF((SELECT ',' + SchemaSmith.fn_SafeBracketWrap([value]) FROM SchemaSmith.fn_SplitList(s.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '' ORDER BY [Ordinal] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),
         s.[ShouldApplyExpression], s.[VariantName]
    INTO #Statistics
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/Statistics') AS Y(st)
    CROSS APPLY (SELECT
      [StatisticName] = st.value('(Name/text())[1]', 'NVARCHAR(500)'),
      [SampleSize] = st.value('(SampleSize/text())[1]', 'TINYINT'),
      [FilterExpression] = st.value('(FilterExpression/text())[1]', 'NVARCHAR(MAX)'),
      [Columns] = st.value('(Columns/text())[1]', 'NVARCHAR(MAX)'),
      [ShouldApplyExpression] = st.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = st.value('(VariantName/text())[1]', 'NVARCHAR(128)')
      ) s;

  -- Identify Statistics to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #Statistics WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Statistics WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  RAISERROR('Parse Full Text Indexes from Xml', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FullTextIndexes
  SELECT [_RowId] = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
         t.[Schema], t.[Name] AS [TableName], [FullTextCatalog] = SchemaSmith.fn_SafeBracketWrap(f.[FullTextCatalog]), [KeyIndex] = SchemaSmith.fn_SafeBracketWrap(f.[KeyIndex]),
         f.[ChangeTracking], [StopList] = SchemaSmith.fn_SafeBracketWrap(COALESCE(NULLIF(RTRIM(f.[StopList]), ''), 'SYSTEM')),
         [Columns] = STUFF((SELECT ',' + SchemaSmith.fn_SafeBracketWrap([value]) FROM SchemaSmith.fn_SplitList(f.[Columns], ',') WHERE SchemaSmith.fn_StripBracketWrapping(RTRIM(LTRIM([Value]))) <> '' ORDER BY [Ordinal] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),
         f.[ShouldApplyExpression], f.[VariantName]
    INTO #FullTextIndexes
    FROM #TableDefinitions t WITH (NOLOCK)
    CROSS APPLY t.[TableXml].nodes('/Table/FullTextIndex') AS Y(ft)
    CROSS APPLY (SELECT
      [Columns] = ft.value('(Columns/text())[1]', 'NVARCHAR(MAX)'),
      [FullTextCatalog] = ft.value('(FullTextCatalog/text())[1]', 'NVARCHAR(500)'),
      [KeyIndex] = ft.value('(KeyIndex/text())[1]', 'NVARCHAR(500)'),
      [ChangeTracking] = ft.value('(ChangeTracking/text())[1]', 'NVARCHAR(500)'),
      [StopList] = ft.value('(StopList/text())[1]', 'NVARCHAR(500)'),
      [ShouldApplyExpression] = ft.value('(ShouldApplyExpression/text())[1]', 'NVARCHAR(MAX)'),
      [VariantName] = ft.value('(VariantName/text())[1]', 'NVARCHAR(128)')
      ) f;

  -- Identify FullTextIndexes to skip based on ShouldApply expression (scoped by [_RowId]) — identical to JSON twin.
  SELECT @v_SQL = STRING_AGG(CAST('DELETE FROM #FullTextIndexes WHERE [_RowId] = ' + CAST([_RowId] AS NVARCHAR(20)) + ' AND NOT (' + SchemaSmith.fn_StripLeadingSelect([ShouldApplyExpression]) + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #FullTextIndexes WITH (NOLOCK)
    WHERE RTRIM(ISNULL([ShouldApplyExpression], '')) <> ''
  EXEC(@v_SQL)

  -- A table with 2+ surviving full-text variants cannot be honored — identical to JSON twin.
  IF EXISTS (SELECT 1 FROM #FullTextIndexes WITH (NOLOCK) GROUP BY [Schema], [TableName] HAVING COUNT(*) > 1)
  BEGIN
    DECLARE @v_FTDupTable NVARCHAR(1010) =
      (SELECT TOP 1 [Schema] + '.' + [TableName] FROM #FullTextIndexes WITH (NOLOCK) GROUP BY [Schema], [TableName] HAVING COUNT(*) > 1 ORDER BY [Schema], [TableName]);
    DECLARE @v_FTDupMsg NVARCHAR(2000) = 'Multiple full-text index variants matched on this target for table ' + @v_FTDupTable +
      '. SQL Server allows one full-text index per table — ShouldApplyExpressions must be mutually exclusive.';
    RAISERROR(@v_FTDupMsg, 16, 1);
  END
