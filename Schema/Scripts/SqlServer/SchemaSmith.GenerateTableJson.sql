-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.GenerateTableJSON', 'P') IS NOT NULL DROP PROCEDURE SchemaSmith.GenerateTableJSON
GO
CREATE PROCEDURE SchemaSmith.GenerateTableJSON 
  @p_Schema SYSNAME = 'dbo',
  @p_Table SYSNAME
AS
SET NOCOUNT ON
DECLARE @v_DatabaseCollation NVARCHAR(200) = CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(200))
-- SchemaSmith-internal extended properties to exclude from extraction (one-line change to add new names)
DECLARE @InternalEPNames TABLE ([Name] NVARCHAR(128))
INSERT @InternalEPNames VALUES (N'ProductName'), (N'PreventDrop')  -- PreventDrop is a SchemaSmith ownership marker, not a user extended property (#270)
SELECT [Line] FROM SchemaSmith.fn_FormatJson(REPLACE(REPLACE(REPLACE((
SELECT '[' + TABLE_SCHEMA + ']' AS [Schema],
       '[' + TABLE_NAME + ']' AS [Name],
       -- sys.partitions is one row PER PARTITION, and compression can legitimately differ across
       -- partitions of the same index -- a scalar read here raised Msg 512 on a partitioned table.
       -- Aggregate instead: a single shared value round-trips as before; non-uniform compression
       -- emits 'MIXED', a sentinel outside Quench's managed NONE/ROW/PAGE/COLUMNSTORE* set so
       -- re-deploy leaves an already-mixed table alone rather than flattening it to one value.
       COALESCE((SELECT CASE COUNT(DISTINCT p.data_compression_desc)
                           WHEN 0 THEN NULL
                           WHEN 1 THEN MIN(p.data_compression_desc)
                           ELSE 'MIXED'
                         END COLLATE DATABASE_DEFAULT
                   FROM sys.partitions AS p WITH (NOLOCK)
                   WHERE p.[object_id] = st.[object_id]
                     AND p.index_id < 2), 'NONE') AS [CompressionType],
       -- Filegroup placement (#filegroups): emit only when the table's data (heap/clustered index,
       -- index_id 0/1) lives on a non-default filegroup, so an ordinary table on PRIMARY (or whatever
       -- the target's default filegroup is) stays exactly as minimal as before this change. Filegroups
       -- predate every supported SQL Server version -- no version gate needed.
       (SELECT '[' + fg.[name] + ']'
          FROM sys.indexes tfg WITH (NOLOCK)
          JOIN sys.filegroups fg WITH (NOLOCK) ON fg.data_space_id = tfg.data_space_id
         WHERE tfg.[object_id] = st.[object_id]
           AND tfg.index_id IN (0, 1)
           AND fg.is_default = 0) AS [FileGroup],
       st.is_tracked_by_cdc AS [EnableCDC],
       -- System-versioning round-trip (#369): emit IsTemporal so an extracted temporal table re-deploys
       -- as temporal (previously omitted -> silently lost on round-trip). Only when true, to keep non-
       -- temporal tables minimal. sys.tables.temporal_type is 2016+ (safe at the current 2017 floor;
       -- gate this + generated_always_type below when the SQL Server floor drops below 2016).
       CASE WHEN st.temporal_type = 2 THEN CAST(1 AS BIT) END AS [IsTemporal],
       -- History table identity/retention (#depth-gap): emit only when they deviate from SchemaSmith's own
       -- apply-side default (same schema, "<Table>_Hist", INFINITE retention) so a default-named temporal
       -- table's JSON stays exactly as minimal as it was before this change. history_table_id /
       -- history_retention_period(_unit_desc) are 2016+ columns -- safe to reference statically at the
       -- current 2017 floor, same reasoning as temporal_type above.
       CASE WHEN st.temporal_type = 2 AND (hs.[name] <> TABLE_SCHEMA OR h.[name] <> TABLE_NAME + '_Hist')
            THEN '[' + hs.[name] + ']' END AS [HistoryTableSchema],
       CASE WHEN st.temporal_type = 2 AND (hs.[name] <> TABLE_SCHEMA OR h.[name] <> TABLE_NAME + '_Hist')
            THEN '[' + h.[name] + ']' END AS [HistoryTableName],
       -- Reads history_retention_period_unit_desc ('DAY'/'WEEK'/'MONTH'/'YEAR'/'INFINITE') rather than the
       -- numeric history_retention_period_unit code: the desc needs no separately-maintained code table
       -- (its 4 finite values pluralize by simple string concatenation), which is exactly what went wrong
       -- here once already -- the numeric codes actually measured on a live server are 3/4/5/6 for
       -- DAY/WEEK/MONTH/YEAR, not the 1/2/3/4 a first pass assumed from documentation. An ELSE branch this
       -- CASE cannot reach today (the 5 values above are exhaustive) still forces a loud runtime error
       -- rather than a silently-dropped-to-NULL retention if Microsoft ever adds a unit: CONVERT(INT,
       -- <text>) always fails to convert non-numeric text, so the surrounding CONVERT(NVARCHAR(10), ...)
       -- keeps this branch's static type consistent with its siblings while still raising Msg 245 with the
       -- offending unit named in the message text.
       CASE WHEN st.temporal_type = 2 AND st.history_retention_period_unit_desc <> 'INFINITE'
            THEN CAST(st.history_retention_period AS NVARCHAR(10)) + ' ' +
                 CASE st.history_retention_period_unit_desc
                   WHEN 'DAY' THEN 'DAYS' WHEN 'WEEK' THEN 'WEEKS' WHEN 'MONTH' THEN 'MONTHS' WHEN 'YEAR' THEN 'YEARS'
                   ELSE CONVERT(NVARCHAR(10), CONVERT(INT, 'Unrecognized SYSTEM_VERSIONING retention unit: ' + ISNULL(st.history_retention_period_unit_desc, CONVERT(NVARCHAR(20), st.history_retention_period_unit))))
                 END
            END AS [HistoryRetentionPeriod],
       -- Emit the sticky drop-protection marker first-class (only when set true, so unprotected tables stay minimal).
       -- Read from the PreventDrop extended property (excluded from generic Extensions via @InternalEPNames). #270
       CASE WHEN (SELECT CONVERT(NVARCHAR(50), [value])
                    FROM fn_listextendedproperty(N'PreventDrop', N'Schema', @p_Schema, N'Table', @p_Table, default, default)) = 'true'
            THEN CAST(1 AS BIT) END AS [PreventDrop],
       '' AS [OldName],
       (SELECT *
          FROM (SELECT '[' + c.COLUMN_NAME + ']' AS [Name],
                       UPPER(USER_TYPE) + SchemaSmith.fn_ColumnTypeArguments(USER_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, DATETIME_PRECISION,
                                               CASE WHEN sc.xml_collection_id <> 0
                                                    THEN (SELECT '[' + SCHEMA_NAME(xc.[schema_id]) + '].[' + xc.[name] + ']' FROM sys.xml_schema_collections xc WHERE xc.xml_collection_id = sc.xml_collection_id)
                                                    END,
                                               sc.is_rowguidcol) +
                                          CASE WHEN ic.column_id IS NOT NULL
                                               THEN ' IDENTITY(' + CONVERT(NVARCHAR(20), ic.seed_value) + ', ' + CONVERT(NVARCHAR(20), ic.increment_value) + ')' +
                                                    CASE WHEN ic.is_not_for_replication = 1 THEN ' NOT FOR REPLICATION' ELSE '' END
                                               ELSE '' END AS [DataType],                   
                       CAST(CASE WHEN c.IS_NULLABLE = 'Yes' THEN 1 ELSE 0 END AS BIT) AS [Nullable],
		               NULLIF(SchemaSmith.fn_StripParenWrapping(COLUMN_DEFAULT), 'NULL') AS [Default],
                       (SELECT SchemaSmith.fn_StripParenWrapping([definition])
                          FROM sys.check_constraints WITH (NOLOCK)
                          WHERE parent_object_id = st.[object_id]
                            AND parent_column_id = sc.column_id) AS [CheckExpression],
                       SchemaSmith.fn_StripParenWrapping(cc.[definition]) AS ComputedExpression,
                       ISNULL(cc.is_persisted, CAST(0 AS BIT)) AS [Persisted],
                       sc.is_sparse AS [Sparse],
                       sc.is_column_set AS [IsColumnSet],
                       ISNULL(NULLIF(ic.COLLATION_NAME, @v_DatabaseCollation), '') AS [Collation],
                       ISNULL(mc.masking_function, '') COLLATE DATABASE_DEFAULT AS DataMaskFunction,
                       ISNULL(sc.encryption_type_desc, 'NONE') COLLATE DATABASE_DEFAULT AS EncryptionType,
                       ISNULL((SELECT '[' + cek.[name] + ']'
                                 FROM sys.column_encryption_keys cek WITH (NOLOCK)
                                WHERE cek.column_encryption_key_id = sc.column_encryption_key_id), '') COLLATE DATABASE_DEFAULT AS EncryptionKey,
                       ISNULL(sc.encryption_algorithm_name, '') COLLATE DATABASE_DEFAULT AS EncryptionAlgorithm,
                       '' AS [OldName],
                       JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Column', c.COLUMN_NAME) x WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames)) + '}}') AS [Extensions]
                  FROM INFORMATION_SCHEMA.COLUMNS c WITH (NOLOCK)
                  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = st.[object_id] AND sc.[name] = c.COLUMN_NAME
                  JOIN (SELECT CASE WHEN SCHEMA_NAME(typ.[schema_id]) IN ('sys', 'dbo')
                                    THEN '' ELSE SCHEMA_NAME(typ.[schema_id]) + '.' END + typ.[name] AS USER_TYPE, typ.user_type_id
                          FROM sys.types typ WITH (NOLOCK)) ut ON ut.user_type_id = sc.user_type_id
                  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[object_id] = st.[object_id]
                                                                 AND cc.[name] = c.COLUMN_NAME
                  LEFT JOIN sys.identity_columns ic WITH (NOLOCK) ON ic.[object_id] = st.[object_id]
                                                                 AND ic.[Name] = c.COLUMN_NAME
                  LEFT JOIN sys.masked_columns mc WITH (NOLOCK) ON mc.[object_id] = st.[object_id]
                                                               AND mc.[name] = c.COLUMN_NAME
                  WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA
                    AND c.TABLE_NAME = t.TABLE_NAME
                    -- Exclude the temporal period columns (GENERATED ALWAYS AS ROW START/END). SchemaSmith
                    -- regenerates ValidFrom/ValidTo from IsTemporal by convention on apply, so emitting them
                    -- as user columns would double-declare them on re-deploy (#369).
                    AND sc.generated_always_type = 0) x
          ORDER BY [Name]
          FOR JSON AUTO) AS [Columns],
       (SELECT '[' + [Name] + ']' AS [Name],
               -- Same per-partition aggregation as the table-level [CompressionType] above.
               (SELECT CASE COUNT(DISTINCT p.data_compression_desc)
                          WHEN 0 THEN NULL
                          WHEN 1 THEN MIN(p.data_compression_desc)
                          ELSE 'MIXED'
                        END COLLATE DATABASE_DEFAULT
                  FROM sys.partitions AS p WITH (NOLOCK)
                  WHERE p.[object_id] = si.[object_id]
                    AND p.index_id = si.index_id) AS [CompressionType],
               -- Same emit-only-when-non-default rule as the table-level [FileGroup] above -- a table and
               -- its indexes are commonly split across filegroups on purpose, so this reads si's own
               -- data_space_id independently of the table's.
               (SELECT '[' + fg.[name] + ']'
                  FROM sys.filegroups fg WITH (NOLOCK)
                 WHERE fg.data_space_id = si.data_space_id
                   AND fg.is_default = 0) AS [FileGroup],
               is_primary_key AS [PrimaryKey],
               is_unique AS [Unique],
               is_unique_constraint AS [UniqueConstraint], 
               CAST(CASE WHEN [type] IN (1, 5) THEN 1 ELSE 0 END AS BIT) AS [Clustered], 
               CAST(CASE WHEN [type] IN (5, 6) THEN 1 ELSE 0 END AS BIT) AS [ColumnStore], 
               CASE WHEN fill_factor = 100 THEN 0 ELSE fill_factor END AS [FillFactor],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY key_ordinal)
                  FROM sys.index_columns ic WITH (NOLOCK)
                  WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 0) AS [IndexColumns],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY index_column_id)
                  FROM sys.index_columns ic WITH (NOLOCK)
                  WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) AS [IncludeColumns],
			   CASE WHEN has_filter = 1 THEN SchemaSmith.fn_StripParenWrapping(filter_definition) ELSE NULL END AS [FilterExpression],
			   JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + [Value] + '"' AS NVARCHAR(MAX)), ',')
                        FROM (SELECT ISNULL(i.[Name], c.[Name]) AS [Name], RTRIM(COALESCE(CONVERT(NVARCHAR(MAX), c.[Value]) + ' ', '') + COALESCE(CONVERT(NVARCHAR(MAX), i.[Value]), '')) AS [Value]
                                FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Index', si.[Name]) i
                                FULL OUTER JOIN fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Constraint', si.[Name]) c ON i.[Name] = c.[Name]) x
                        WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames))
                   + '}}') AS [Extensions]
          FROM sys.indexes si WITH (NOLOCK)
          WHERE si.[object_id] = st.[object_id]
            AND NOT EXISTS (SELECT * FROM sys.xml_indexes xi WITH (NOLOCK) WHERE xi.[object_id] = si.[object_id] AND xi.index_id = si.index_id)
            AND is_hypothetical = 0
            AND is_disabled = 0
            AND index_id > 0
          ORDER BY [Name]
          FOR JSON AUTO) AS [Indexes],
       (SELECT '[' + i.[name] COLLATE DATABASE_DEFAULT + ']' AS [Name],
               '[' + COL_NAME(i.[Object_id], ic.column_id) + ']' AS [Column],
               CONVERT(BIT, CASE WHEN i.xml_index_type = 0 THEN 1 ELSE 0 END) AS [IsPrimary],
               (SELECT '[' + [Name] COLLATE DATABASE_DEFAULT + ']' FROM sys.xml_indexes i2 WHERE i2.[object_id] = i.[object_id] AND i2.index_id = i.using_xml_index_id AND i.xml_index_type = 1) AS [PrimaryIndex],
               i.secondary_type_desc COLLATE DATABASE_DEFAULT AS [SecondaryIndexType],
			   JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + x.[Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Index', i.[Name]) x WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames)) + '}}') AS [Extensions]
          FROM sys.xml_indexes i WITH (NOLOCK)
          JOIN sys.index_columns ic WITH (NOLOCK) ON i.[object_id] = ic.[object_id] AND i.index_id = ic.index_id
          WHERE i.[object_id] = st.[object_id]
          ORDER BY i.[Name]
          FOR JSON AUTO) AS [XmlIndexes],
	   (SELECT '[' + [Name] + ']' AS [Name],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[parent_object_id], fc.parent_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                            FROM sys.foreign_key_columns fc WITH (NOLOCK)
                            WHERE fk.[object_id] = fc.[constraint_object_id]) AS [Columns],
               '[' + OBJECT_SCHEMA_NAME(referenced_object_id) + ']' AS RelatedTableSchema,
               '[' + OBJECT_NAME(referenced_object_id) + ']' AS RelatedTable,
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[referenced_object_id], fc.referenced_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                            FROM sys.foreign_key_columns fc WITH (NOLOCK)
                            WHERE fk.[object_id] = fc.[constraint_object_id]) AS [RelatedColumns],
               REPLACE(fk.delete_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT AS [DeleteAction],
               REPLACE(fk.update_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT AS [UpdateAction],
               JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Constraint', fk.[Name]) x WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames)) + '}}') AS [Extensions]
          FROM sys.foreign_keys fk WITH (NOLOCK)
          WHERE fk.parent_object_id = st.[object_id]
          ORDER BY [Name]
          FOR JSON AUTO) AS [ForeignKeys],
       (SELECT '[' + [Name] + ']' AS [Name], 
               (SELECT STRING_AGG(CAST('[' + COL_NAME(sc.[object_id], sc.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY sc.stats_column_id)
                  FROM sys.stats_columns sc WITH (NOLOCK)
                  WHERE s.[object_id] = sc.[object_id] AND s.stats_id = sc.stats_id) AS [Columns],
               SchemaSmith.fn_StripParenWrapping([filter_definition]) AS FilterExpression,
			   JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Statistic', s.[Name]) x WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames)) + '}}') AS [Extensions]
          FROM sys.stats s WITH (NOLOCK)
          WHERE [object_id] = st.[object_id]
            AND auto_created = 0
            AND user_created = 1
            AND is_temporary = 0
            AND [Name] NOT LIKE 'stat[_]%'
            AND [Name] NOT LIKE 'hind[_]%'
          ORDER BY [Name]
          FOR JSON AUTO) AS [Statistics],
       (SELECT '[' + [Name] + ']' AS [Name],
               SchemaSmith.fn_StripParenWrapping([definition]) AS [Expression],
               JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Constraint', cc.[Name]) x WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames)) + '}}') AS [Extensions]
          FROM sys.check_constraints cc WITH (NOLOCK)
          WHERE parent_object_id = st.[object_id]
            AND parent_column_id = 0
          ORDER BY [Name]
          FOR JSON AUTO) AS [CheckConstraints],
       (SELECT FullTextCatalog = '[' + (SELECT c.[name] FROM sys.fulltext_catalogs c WITH (NOLOCK) WHERE c.fulltext_catalog_id = fi.fulltext_catalog_id) + ']',
               KeyIndex = '[' + (SELECT i.[Name] FROM sys.indexes i WITH (NOLOCK) WHERE i.[object_id] = fi.[object_id] AND i.[index_id] = fi.[unique_index_id]) + ']',
               ChangeTracking = change_tracking_state_desc,
               [StopList] = '[' + (SELECT fs.[name] FROM sys.fulltext_stoplists fs WITH (NOLOCK) WHERE fs.stoplist_id = fi.stoplist_id) + ']',
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[object_id], fc.column_id) + ']' +
                                       CASE WHEN fc.type_column_id IS NOT NULL
                                            THEN ' TYPE COLUMN [' + COL_NAME(fc.[object_id], fc.type_column_id) + ']'
                                            ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(fc.[object_id], fc.column_id))
                  FROM sys.fulltext_index_columns fc WITH (NOLOCK)
                  WHERE fi.[object_id] = fc.[object_id]) AS [Columns]
          FROM sys.fulltext_indexes fi WITH (NOLOCK)
          WHERE fi.[object_id] = st.[object_id]
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [FullTextIndex],
	   JSON_QUERY('{"ExtendedProperties": {' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, default, default) x WHERE x.[Name] COLLATE DATABASE_DEFAULT NOT IN (SELECT [Name] FROM @InternalEPNames)) + '}}') AS [Extensions]
  FROM INFORMATION_SCHEMA.TABLES t WITH (NOLOCK)
  JOIN sys.tables st WITH (NOLOCK) ON st.[object_id] = OBJECT_ID(@p_Schema + '.' + @p_Table)
  LEFT JOIN sys.tables h WITH (NOLOCK) ON h.[object_id] = st.history_table_id
  LEFT JOIN sys.schemas hs WITH (NOLOCK) ON hs.[schema_id] = h.[schema_id]
  WHERE TABLE_NAME = @p_Table
    AND TABLE_SCHEMA = @p_Schema
  FOR JSON AUTO, WITHOUT_ARRAY_WRAPPER
), '\"', '"'), '"}"', '" }'), '"{"', '{ "'), 1)
ORDER BY [LineNo]