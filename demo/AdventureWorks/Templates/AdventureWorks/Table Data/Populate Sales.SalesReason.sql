
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Price","ReasonType":"Other","SalesReasonID":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"On Promotion","ReasonType":"Promotion","SalesReasonID":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Magazine Advertisement","ReasonType":"Marketing","SalesReasonID":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Television  Advertisement","ReasonType":"Marketing","SalesReasonID":4},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Manufacturer","ReasonType":"Other","SalesReasonID":5},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Review","ReasonType":"Other","SalesReasonID":6},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Demo Event","ReasonType":"Marketing","SalesReasonID":7},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Sponsorship","ReasonType":"Marketing","SalesReasonID":8},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Quality","ReasonType":"Other","SalesReasonID":9},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Other","ReasonType":"Other","SalesReasonID":10}
]';

ALTER TABLE [Sales].[SalesReason] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [Sales].[SalesReason] ON; 
MERGE INTO [Sales].[SalesReason] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ReasonType],[SalesReasonID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ReasonType] NAME,
           [SalesReasonID] INT
    )
) AS Source
ON Source.[SalesReasonID] = Target.[SalesReasonID]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[ReasonType] = Source.[ReasonType] OR (Target.[ReasonType] IS NULL AND Source.[ReasonType] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ReasonType] = Source.[ReasonType]


WHEN NOT MATCHED THEN
  INSERT (
        [ModifiedDate],
        [Name],
        [ReasonType],
        [SalesReasonID]
  ) VALUES (
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ReasonType],
        Source.[SalesReasonID]  
  )
;
SET IDENTITY_INSERT [Sales].[SalesReason] OFF;
ALTER TABLE [Sales].[SalesReason] ENABLE TRIGGER ALL;
