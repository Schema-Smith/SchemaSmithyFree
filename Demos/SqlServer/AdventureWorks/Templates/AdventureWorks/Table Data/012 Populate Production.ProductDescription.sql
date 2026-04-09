
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductDescription.tabledata}}';


SET IDENTITY_INSERT [Production].[ProductDescription] ON;
MERGE INTO [Production].[ProductDescription] AS Target
USING (
  SELECT [Description],[ModifiedDate],[ProductDescriptionID]
    FROM OPENJSON(@v_json)
    WITH (
           [Description] NVARCHAR(400),
           [ModifiedDate] DATETIME,
           [ProductDescriptionID] INT,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[ProductDescriptionID] = Target.[ProductDescriptionID]

WHEN MATCHED AND (NOT (Target.[Description] = Source.[Description] OR (Target.[Description] IS NULL AND Source.[Description] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [Description] = Source.[Description],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Description],
        [ModifiedDate],
        [ProductDescriptionID]
   ) VALUES (
         Source.[Description],
        Source.[ModifiedDate],
        Source.[ProductDescriptionID]
   )
 ;
SET IDENTITY_INSERT [Production].[ProductDescription] OFF;
