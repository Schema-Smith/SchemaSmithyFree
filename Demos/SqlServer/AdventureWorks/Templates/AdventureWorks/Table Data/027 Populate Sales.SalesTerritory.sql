
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesTerritory.tabledata}}';


SET IDENTITY_INSERT [Sales].[SalesTerritory] ON;
MERGE INTO [Sales].[SalesTerritory] AS Target
USING (
  SELECT [CostLastYear],[CostYTD],[CountryRegionCode],[Group],[ModifiedDate],[Name],[SalesLastYear],[SalesYTD],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [CostLastYear] MONEY,
           [CostYTD] MONEY,
           [CountryRegionCode] NVARCHAR(3),
           [Group] NVARCHAR(50),
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesLastYear] MONEY,
           [SalesYTD] MONEY,
           [TerritoryID] INT
    )
) AS Source
ON Source.[TerritoryID] = Target.[TerritoryID]

WHEN MATCHED AND (NOT (Target.[CostLastYear] = Source.[CostLastYear] OR (Target.[CostLastYear] IS NULL AND Source.[CostLastYear] IS NULL)) OR NOT (Target.[CostYTD] = Source.[CostYTD] OR (Target.[CostYTD] IS NULL AND Source.[CostYTD] IS NULL)) OR NOT (Target.[CountryRegionCode] = Source.[CountryRegionCode] OR (Target.[CountryRegionCode] IS NULL AND Source.[CountryRegionCode] IS NULL)) OR NOT (Target.[Group] = Source.[Group] OR (Target.[Group] IS NULL AND Source.[Group] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[SalesLastYear] = Source.[SalesLastYear] OR (Target.[SalesLastYear] IS NULL AND Source.[SalesLastYear] IS NULL)) OR NOT (Target.[SalesYTD] = Source.[SalesYTD] OR (Target.[SalesYTD] IS NULL AND Source.[SalesYTD] IS NULL))) THEN
  UPDATE SET
        [CostLastYear] = Source.[CostLastYear],
        [CostYTD] = Source.[CostYTD],
        [CountryRegionCode] = Source.[CountryRegionCode],
        [Group] = Source.[Group],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [SalesLastYear] = Source.[SalesLastYear],
        [SalesYTD] = Source.[SalesYTD]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CostLastYear],
        [CostYTD],
        [CountryRegionCode],
        [Group],
        [ModifiedDate],
        [Name],
        [SalesLastYear],
        [SalesYTD],
        [TerritoryID]
   ) VALUES (
         Source.[CostLastYear],
        Source.[CostYTD],
        Source.[CountryRegionCode],
        Source.[Group],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[SalesLastYear],
        Source.[SalesYTD],
        Source.[TerritoryID]
   )
 ;
SET IDENTITY_INSERT [Sales].[SalesTerritory] OFF;
