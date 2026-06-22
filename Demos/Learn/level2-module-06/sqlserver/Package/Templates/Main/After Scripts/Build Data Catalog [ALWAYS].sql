-- Reads the full model from the TableSchema system token (Module 4) — which includes the
-- custom Extensions metadata you attached — and rebuilds DataCatalog from it on every deploy.
-- Walks tables then columns; records each column's Classification and its table's OwningTeam,
-- for every table that carries an OwningTeam tag. Tables without the tag (like DataCatalog
-- itself) are skipped. Rebuilt from the model each run, so the catalog is always current.
DELETE FROM [dbo].[DataCatalog];

INSERT INTO [dbo].[DataCatalog] ([TableName], [ColumnName], [Classification], [OwningTeam])
SELECT t.[TableName], c.[ColumnName], c.[Classification], t.[OwningTeam]
  FROM OPENJSON(N'{{TableSchema}}')
       WITH (
         [TableName]  NVARCHAR(256) '$.Name',
         [OwningTeam] NVARCHAR(64)  '$.Extensions.OwningTeam',
         [Columns]    NVARCHAR(MAX) '$.Columns' AS JSON
       ) t
 CROSS APPLY OPENJSON(t.[Columns])
       WITH (
         [ColumnName]     NVARCHAR(256) '$.Name',
         [Classification] NVARCHAR(64)  '$.Extensions.Classification'
       ) c
 WHERE t.[OwningTeam] IS NOT NULL;
