
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductListPriceHistory.tabledata}}';



MERGE INTO [Production].[ProductListPriceHistory] AS Target
USING (
  SELECT [EndDate],[ListPrice],[ModifiedDate],[ProductID],[StartDate]
    FROM OPENJSON(@v_json)
    WITH (
           [EndDate] DATETIME,
           [ListPrice] MONEY,
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [StartDate] DATETIME
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID] AND Source.[StartDate] = Target.[StartDate]

WHEN MATCHED AND (NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[ListPrice] = Source.[ListPrice] OR (Target.[ListPrice] IS NULL AND Source.[ListPrice] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL))) THEN
  UPDATE SET
        [EndDate] = Source.[EndDate],
        [ListPrice] = Source.[ListPrice],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [StartDate] = Source.[StartDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [EndDate],
        [ListPrice],
        [ModifiedDate],
        [ProductID],
        [StartDate]
   ) VALUES (
         Source.[EndDate],
        Source.[ListPrice],
        Source.[ModifiedDate],
        Source.[ProductID],
        Source.[StartDate]
   )
 ;
