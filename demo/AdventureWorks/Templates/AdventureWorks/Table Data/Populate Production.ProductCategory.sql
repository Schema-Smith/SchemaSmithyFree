
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bikes","ProductCategoryID":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Components","ProductCategoryID":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Clothing","ProductCategoryID":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Accessories","ProductCategoryID":4}
]';

ALTER TABLE [Production].[ProductCategory] DISABLE TRIGGER ALL;
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


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
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
ALTER TABLE [Production].[ProductCategory] ENABLE TRIGGER ALL;
