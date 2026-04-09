
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesPerson.tabledata}}';



MERGE INTO [Sales].[SalesPerson] AS Target
USING (
  SELECT [Bonus],[BusinessEntityID],[CommissionPct],[ModifiedDate],[SalesLastYear],[SalesQuota],[SalesYTD],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [Bonus] MONEY,
           [BusinessEntityID] INT,
           [CommissionPct] SMALLMONEY,
           [ModifiedDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesLastYear] MONEY,
           [SalesQuota] MONEY,
           [SalesYTD] MONEY,
           [TerritoryID] INT
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[Bonus] = Source.[Bonus] OR (Target.[Bonus] IS NULL AND Source.[Bonus] IS NULL)) OR NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[CommissionPct] = Source.[CommissionPct] OR (Target.[CommissionPct] IS NULL AND Source.[CommissionPct] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[SalesLastYear] = Source.[SalesLastYear] OR (Target.[SalesLastYear] IS NULL AND Source.[SalesLastYear] IS NULL)) OR NOT (Target.[SalesQuota] = Source.[SalesQuota] OR (Target.[SalesQuota] IS NULL AND Source.[SalesQuota] IS NULL)) OR NOT (Target.[SalesYTD] = Source.[SalesYTD] OR (Target.[SalesYTD] IS NULL AND Source.[SalesYTD] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [Bonus] = Source.[Bonus],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [CommissionPct] = Source.[CommissionPct],
        [ModifiedDate] = Source.[ModifiedDate],
        [SalesLastYear] = Source.[SalesLastYear],
        [SalesQuota] = Source.[SalesQuota],
        [SalesYTD] = Source.[SalesYTD],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Bonus],
        [BusinessEntityID],
        [CommissionPct],
        [ModifiedDate],
        [SalesLastYear],
        [SalesQuota],
        [SalesYTD],
        [TerritoryID]
   ) VALUES (
         Source.[Bonus],
        Source.[BusinessEntityID],
        Source.[CommissionPct],
        Source.[ModifiedDate],
        Source.[SalesLastYear],
        Source.[SalesQuota],
        Source.[SalesYTD],
        Source.[TerritoryID]
   )
 ;
