
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductCategory.tabledata}}';


SET IDENTITY_INSERT [Production].[ProductCategory] ON;
MERGE INTO [Production].[ProductCategory] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ProductCategoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ProductCategoryID] INT,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[ProductCategoryID] = Target.[ProductCategoryID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [ProductCategoryID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[ProductCategoryID]
   )
 ;
SET IDENTITY_INSERT [Production].[ProductCategory] OFF;
