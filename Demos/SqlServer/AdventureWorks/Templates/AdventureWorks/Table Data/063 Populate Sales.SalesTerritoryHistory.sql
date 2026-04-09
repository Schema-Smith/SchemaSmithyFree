
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesTerritoryHistory.tabledata}}';



MERGE INTO [Sales].[SalesTerritoryHistory] AS Target
USING (
  SELECT [BusinessEntityID],[EndDate],[ModifiedDate],[StartDate],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [EndDate] DATETIME,
           [ModifiedDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER,
           [StartDate] DATETIME,
           [TerritoryID] INT
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[StartDate] = Target.[StartDate] AND Source.[TerritoryID] = Target.[TerritoryID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [EndDate] = Source.[EndDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [StartDate] = Source.[StartDate],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [EndDate],
        [ModifiedDate],
        [StartDate],
        [TerritoryID]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[EndDate],
        Source.[ModifiedDate],
        Source.[StartDate],
        Source.[TerritoryID]
   )
 ;
