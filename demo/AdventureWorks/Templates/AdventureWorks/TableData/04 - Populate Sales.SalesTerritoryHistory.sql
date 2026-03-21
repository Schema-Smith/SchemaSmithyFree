DECLARE @v_json NVARCHAR(MAX) = '[
{"BusinessEntityID":275,"EndDate":"2012-11-29T00:00:00","ModifiedDate":"2012-11-22T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":2},
{"BusinessEntityID":275,"ModifiedDate":"2012-11-23T00:00:00","StartDate":"2012-11-30T00:00:00","TerritoryID":3},
{"BusinessEntityID":276,"ModifiedDate":"2011-05-24T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":4},
{"BusinessEntityID":277,"EndDate":"2012-11-29T00:00:00","ModifiedDate":"2012-11-22T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":3},
{"BusinessEntityID":277,"ModifiedDate":"2012-11-23T00:00:00","StartDate":"2012-11-30T00:00:00","TerritoryID":2},
{"BusinessEntityID":278,"ModifiedDate":"2011-05-24T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":6},
{"BusinessEntityID":279,"ModifiedDate":"2011-05-24T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":5},
{"BusinessEntityID":280,"EndDate":"2012-09-29T00:00:00","ModifiedDate":"2012-09-22T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":1},
{"BusinessEntityID":281,"ModifiedDate":"2011-05-24T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":4},
{"BusinessEntityID":282,"EndDate":"2012-05-29T00:00:00","ModifiedDate":"2012-05-22T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":6},
{"BusinessEntityID":282,"ModifiedDate":"2012-05-23T00:00:00","StartDate":"2012-05-30T00:00:00","TerritoryID":10},
{"BusinessEntityID":283,"ModifiedDate":"2011-05-24T00:00:00","StartDate":"2011-05-31T00:00:00","TerritoryID":1},
{"BusinessEntityID":284,"ModifiedDate":"2012-09-23T00:00:00","StartDate":"2012-09-30T00:00:00","TerritoryID":1},
{"BusinessEntityID":286,"ModifiedDate":"2013-05-23T00:00:00","StartDate":"2013-05-30T00:00:00","TerritoryID":9},
{"BusinessEntityID":288,"ModifiedDate":"2013-05-23T00:00:00","StartDate":"2013-05-30T00:00:00","TerritoryID":8},
{"BusinessEntityID":289,"ModifiedDate":"2012-05-23T00:00:00","StartDate":"2012-05-30T00:00:00","TerritoryID":6},
{"BusinessEntityID":290,"ModifiedDate":"2012-05-23T00:00:00","StartDate":"2012-05-30T00:00:00","TerritoryID":7}
]';

 
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
WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) AND NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL)) AND NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [EndDate] = Source.[EndDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [StartDate] = Source.[StartDate],
        [TerritoryID] = Source.[TerritoryID]
WHEN NOT MATCHED THEN
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
  );
 
