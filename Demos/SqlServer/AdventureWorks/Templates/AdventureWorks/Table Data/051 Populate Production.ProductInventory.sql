
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductInventory.tabledata}}';



MERGE INTO [Production].[ProductInventory] AS Target
USING (
  SELECT [Bin],[LocationID],[ModifiedDate],[ProductID],[Quantity],[Shelf]
    FROM OPENJSON(@v_json)
    WITH (
           [Bin] TINYINT,
           [LocationID] SMALLINT,
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [Quantity] SMALLINT,
           [rowguid] UNIQUEIDENTIFIER,
           [Shelf] NVARCHAR(10)
    )
) AS Source
ON Source.[LocationID] = Target.[LocationID] AND Source.[ProductID] = Target.[ProductID]

WHEN MATCHED AND (NOT (Target.[Bin] = Source.[Bin] OR (Target.[Bin] IS NULL AND Source.[Bin] IS NULL)) OR NOT (Target.[LocationID] = Source.[LocationID] OR (Target.[LocationID] IS NULL AND Source.[LocationID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[Quantity] = Source.[Quantity] OR (Target.[Quantity] IS NULL AND Source.[Quantity] IS NULL)) OR NOT (Target.[Shelf] = Source.[Shelf] OR (Target.[Shelf] IS NULL AND Source.[Shelf] IS NULL))) THEN
  UPDATE SET
        [Bin] = Source.[Bin],
        [LocationID] = Source.[LocationID],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [Quantity] = Source.[Quantity],
        [Shelf] = Source.[Shelf]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Bin],
        [LocationID],
        [ModifiedDate],
        [ProductID],
        [Quantity],
        [Shelf]
   ) VALUES (
         Source.[Bin],
        Source.[LocationID],
        Source.[ModifiedDate],
        Source.[ProductID],
        Source.[Quantity],
        Source.[Shelf]
   )
 ;
