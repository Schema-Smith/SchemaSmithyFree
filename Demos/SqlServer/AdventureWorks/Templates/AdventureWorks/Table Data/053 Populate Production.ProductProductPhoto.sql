
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductProductPhoto.tabledata}}';



MERGE INTO [Production].[ProductProductPhoto] AS Target
USING (
  SELECT [ModifiedDate],[Primary],[ProductID],[ProductPhotoID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Primary] FLAG,
           [ProductID] INT,
           [ProductPhotoID] INT
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID] AND Source.[ProductPhotoID] = Target.[ProductPhotoID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Primary] = Source.[Primary] OR (Target.[Primary] IS NULL AND Source.[Primary] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[ProductPhotoID] = Source.[ProductPhotoID] OR (Target.[ProductPhotoID] IS NULL AND Source.[ProductPhotoID] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Primary] = Source.[Primary],
        [ProductID] = Source.[ProductID],
        [ProductPhotoID] = Source.[ProductPhotoID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Primary],
        [ProductID],
        [ProductPhotoID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Primary],
        Source.[ProductID],
        Source.[ProductPhotoID]
   )
 ;
