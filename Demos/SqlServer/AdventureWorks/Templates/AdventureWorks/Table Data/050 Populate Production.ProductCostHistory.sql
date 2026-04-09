
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductCostHistory.tabledata}}';



MERGE INTO [Production].[ProductCostHistory] AS Target
USING (
  SELECT [EndDate],[ModifiedDate],[ProductID],[StandardCost],[StartDate]
    FROM OPENJSON(@v_json)
    WITH (
           [EndDate] DATETIME,
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [StandardCost] MONEY,
           [StartDate] DATETIME
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID] AND Source.[StartDate] = Target.[StartDate]

WHEN MATCHED AND (NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[StandardCost] = Source.[StandardCost] OR (Target.[StandardCost] IS NULL AND Source.[StandardCost] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL))) THEN
  UPDATE SET
        [EndDate] = Source.[EndDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [StandardCost] = Source.[StandardCost],
        [StartDate] = Source.[StartDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [EndDate],
        [ModifiedDate],
        [ProductID],
        [StandardCost],
        [StartDate]
   ) VALUES (
         Source.[EndDate],
        Source.[ModifiedDate],
        Source.[ProductID],
        Source.[StandardCost],
        Source.[StartDate]
   )
 ;
