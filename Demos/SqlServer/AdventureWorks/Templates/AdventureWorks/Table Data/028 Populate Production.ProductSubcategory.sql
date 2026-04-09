
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductSubcategory.tabledata}}';


SET IDENTITY_INSERT [Production].[ProductSubcategory] ON;
MERGE INTO [Production].[ProductSubcategory] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ProductCategoryID],[ProductSubcategoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ProductCategoryID] INT,
           [ProductSubcategoryID] INT,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[ProductSubcategoryID] = Target.[ProductSubcategoryID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[ProductCategoryID] = Source.[ProductCategoryID] OR (Target.[ProductCategoryID] IS NULL AND Source.[ProductCategoryID] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ProductCategoryID] = Source.[ProductCategoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [ProductCategoryID],
        [ProductSubcategoryID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[ProductCategoryID],
        Source.[ProductSubcategoryID]
   )
 ;
SET IDENTITY_INSERT [Production].[ProductSubcategory] OFF;
