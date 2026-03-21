DECLARE @v_json NVARCHAR(MAX) = '[
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"US","Group":"North America","ModifiedDate":"2008-04-30T00:00:00","Name":"Northwest","SalesLastYear":3298694.4938,"SalesYTD":7887186.7882,"TerritoryID":1},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"US","Group":"North America","ModifiedDate":"2008-04-30T00:00:00","Name":"Northeast","SalesLastYear":3607148.9371,"SalesYTD":2402176.8476,"TerritoryID":2},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"US","Group":"North America","ModifiedDate":"2008-04-30T00:00:00","Name":"Central","SalesLastYear":3205014.0767,"SalesYTD":3072175.1180,"TerritoryID":3},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"US","Group":"North America","ModifiedDate":"2008-04-30T00:00:00","Name":"Southwest","SalesLastYear":5366575.7098,"SalesYTD":10510853.8739,"TerritoryID":4},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"US","Group":"North America","ModifiedDate":"2008-04-30T00:00:00","Name":"Southeast","SalesLastYear":3925071.4318,"SalesYTD":2538667.2515,"TerritoryID":5},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"CA","Group":"North America","ModifiedDate":"2008-04-30T00:00:00","Name":"Canada","SalesLastYear":5693988.8600,"SalesYTD":6771829.1376,"TerritoryID":6},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"FR","Group":"Europe","ModifiedDate":"2008-04-30T00:00:00","Name":"France","SalesLastYear":2396539.7601,"SalesYTD":4772398.3078,"TerritoryID":7},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"DE","Group":"Europe","ModifiedDate":"2008-04-30T00:00:00","Name":"Germany","SalesLastYear":1307949.7917,"SalesYTD":3805202.3478,"TerritoryID":8},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"AU","Group":"Pacific","ModifiedDate":"2008-04-30T00:00:00","Name":"Australia","SalesLastYear":2278548.9776,"SalesYTD":5977814.9154,"TerritoryID":9},
{"CostLastYear":0.0000,"CostYTD":0.0000,"CountryRegionCode":"GB","Group":"Europe","ModifiedDate":"2008-04-30T00:00:00","Name":"United Kingdom","SalesLastYear":1635823.3967,"SalesYTD":5012905.3656,"TerritoryID":10}
]';

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
WHEN MATCHED AND (NOT (Target.[CostLastYear] = Source.[CostLastYear] OR (Target.[CostLastYear] IS NULL AND Source.[CostLastYear] IS NULL)) AND NOT (Target.[CostYTD] = Source.[CostYTD] OR (Target.[CostYTD] IS NULL AND Source.[CostYTD] IS NULL)) AND NOT (Target.[CountryRegionCode] = Source.[CountryRegionCode] OR (Target.[CountryRegionCode] IS NULL AND Source.[CountryRegionCode] IS NULL)) AND NOT (Target.[Group] = Source.[Group] OR (Target.[Group] IS NULL AND Source.[Group] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[SalesLastYear] = Source.[SalesLastYear] OR (Target.[SalesLastYear] IS NULL AND Source.[SalesLastYear] IS NULL)) AND NOT (Target.[SalesYTD] = Source.[SalesYTD] OR (Target.[SalesYTD] IS NULL AND Source.[SalesYTD] IS NULL))) THEN
  UPDATE SET
        [CostLastYear] = Source.[CostLastYear],
        [CostYTD] = Source.[CostYTD],
        [CountryRegionCode] = Source.[CountryRegionCode],
        [Group] = Source.[Group],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [SalesLastYear] = Source.[SalesLastYear],
        [SalesYTD] = Source.[SalesYTD]
WHEN NOT MATCHED THEN
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
  );
SET IDENTITY_INSERT [Sales].[SalesTerritory] OFF; 
