DECLARE @v_json NVARCHAR(MAX) = '[
{"Bonus":0.0000,"BusinessEntityID":274,"CommissionPct":0.0000,"ModifiedDate":"2010-12-28T00:00:00","SalesLastYear":0.0000,"SalesYTD":559697.5639},
{"Bonus":4100.0000,"BusinessEntityID":275,"CommissionPct":0.0120,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1750406.4785,"SalesQuota":300000.0000,"SalesYTD":3763178.1787,"TerritoryID":2},
{"Bonus":2000.0000,"BusinessEntityID":276,"CommissionPct":0.0150,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1439156.0291,"SalesQuota":250000.0000,"SalesYTD":4251368.5497,"TerritoryID":4},
{"Bonus":2500.0000,"BusinessEntityID":277,"CommissionPct":0.0150,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1997186.2037,"SalesQuota":250000.0000,"SalesYTD":3189418.3662,"TerritoryID":3},
{"Bonus":500.0000,"BusinessEntityID":278,"CommissionPct":0.0100,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1620276.8966,"SalesQuota":250000.0000,"SalesYTD":1453719.4653,"TerritoryID":6},
{"Bonus":6700.0000,"BusinessEntityID":279,"CommissionPct":0.0100,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1849640.9418,"SalesQuota":300000.0000,"SalesYTD":2315185.6110,"TerritoryID":5},
{"Bonus":5000.0000,"BusinessEntityID":280,"CommissionPct":0.0100,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1927059.1780,"SalesQuota":250000.0000,"SalesYTD":1352577.1325,"TerritoryID":1},
{"Bonus":3550.0000,"BusinessEntityID":281,"CommissionPct":0.0100,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":2073505.9999,"SalesQuota":250000.0000,"SalesYTD":2458535.6169,"TerritoryID":4},
{"Bonus":5000.0000,"BusinessEntityID":282,"CommissionPct":0.0150,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":2038234.6549,"SalesQuota":250000.0000,"SalesYTD":2604540.7172,"TerritoryID":6},
{"Bonus":3500.0000,"BusinessEntityID":283,"CommissionPct":0.0120,"ModifiedDate":"2011-05-24T00:00:00","SalesLastYear":1371635.3158,"SalesQuota":250000.0000,"SalesYTD":1573012.9383,"TerritoryID":1},
{"Bonus":3900.0000,"BusinessEntityID":284,"CommissionPct":0.0190,"ModifiedDate":"2012-09-23T00:00:00","SalesLastYear":0.0000,"SalesQuota":300000.0000,"SalesYTD":1576562.1966,"TerritoryID":1},
{"Bonus":0.0000,"BusinessEntityID":285,"CommissionPct":0.0000,"ModifiedDate":"2013-03-07T00:00:00","SalesLastYear":0.0000,"SalesYTD":172524.4512},
{"Bonus":5650.0000,"BusinessEntityID":286,"CommissionPct":0.0180,"ModifiedDate":"2013-05-23T00:00:00","SalesLastYear":2278548.9776,"SalesQuota":250000.0000,"SalesYTD":1421810.9242,"TerritoryID":9},
{"Bonus":0.0000,"BusinessEntityID":287,"CommissionPct":0.0000,"ModifiedDate":"2012-04-09T00:00:00","SalesLastYear":0.0000,"SalesYTD":519905.9320},
{"Bonus":75.0000,"BusinessEntityID":288,"CommissionPct":0.0180,"ModifiedDate":"2013-05-23T00:00:00","SalesLastYear":1307949.7917,"SalesQuota":250000.0000,"SalesYTD":1827066.7118,"TerritoryID":8},
{"Bonus":5150.0000,"BusinessEntityID":289,"CommissionPct":0.0200,"ModifiedDate":"2012-05-23T00:00:00","SalesLastYear":1635823.3967,"SalesQuota":250000.0000,"SalesYTD":4116871.2277,"TerritoryID":10},
{"Bonus":985.0000,"BusinessEntityID":290,"CommissionPct":0.0160,"ModifiedDate":"2012-05-23T00:00:00","SalesLastYear":2396539.7601,"SalesQuota":250000.0000,"SalesYTD":3121616.3202,"TerritoryID":7}
]';

 
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
WHEN MATCHED AND (NOT (Target.[Bonus] = Source.[Bonus] OR (Target.[Bonus] IS NULL AND Source.[Bonus] IS NULL)) AND NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) AND NOT (Target.[CommissionPct] = Source.[CommissionPct] OR (Target.[CommissionPct] IS NULL AND Source.[CommissionPct] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[SalesLastYear] = Source.[SalesLastYear] OR (Target.[SalesLastYear] IS NULL AND Source.[SalesLastYear] IS NULL)) AND NOT (Target.[SalesQuota] = Source.[SalesQuota] OR (Target.[SalesQuota] IS NULL AND Source.[SalesQuota] IS NULL)) AND NOT (Target.[SalesYTD] = Source.[SalesYTD] OR (Target.[SalesYTD] IS NULL AND Source.[SalesYTD] IS NULL)) AND NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [Bonus] = Source.[Bonus],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [CommissionPct] = Source.[CommissionPct],
        [ModifiedDate] = Source.[ModifiedDate],
        [SalesLastYear] = Source.[SalesLastYear],
        [SalesQuota] = Source.[SalesQuota],
        [SalesYTD] = Source.[SalesYTD],
        [TerritoryID] = Source.[TerritoryID]
WHEN NOT MATCHED THEN
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
  );
 
