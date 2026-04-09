
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductModel.tabledata}}';


SET IDENTITY_INSERT [Production].[ProductModel] ON;
MERGE INTO [Production].[ProductModel] AS Target
USING (
  SELECT [CatalogDescription],[Instructions],[ModifiedDate],[Name],[ProductModelID]
    FROM OPENJSON(@v_json)
    WITH (
           [CatalogDescription] XML([Production].[ProductDescriptionSchemaCollection]),
           [Instructions] XML([Production].[ManuInstructionsSchemaCollection]),
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ProductModelID] INT,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[ProductModelID] = Target.[ProductModelID]

WHEN MATCHED AND (NOT (CAST(Target.[CatalogDescription] AS NVARCHAR(MAX)) = CAST(Source.[CatalogDescription] AS NVARCHAR(MAX)) OR (Target.[CatalogDescription] IS NULL AND Source.[CatalogDescription] IS NULL)) OR NOT (CAST(Target.[Instructions] AS NVARCHAR(MAX)) = CAST(Source.[Instructions] AS NVARCHAR(MAX)) OR (Target.[Instructions] IS NULL AND Source.[Instructions] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [CatalogDescription] = Source.[CatalogDescription],
        [Instructions] = Source.[Instructions],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CatalogDescription],
        [Instructions],
        [ModifiedDate],
        [Name],
        [ProductModelID]
   ) VALUES (
         Source.[CatalogDescription],
        Source.[Instructions],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ProductModelID]
   )
 ;
SET IDENTITY_INSERT [Production].[ProductModel] OFF;
