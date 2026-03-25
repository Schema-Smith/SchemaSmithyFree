
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Brake assembly not as ordered","ScrapReasonID":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Color incorrect","ScrapReasonID":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Gouge in metal","ScrapReasonID":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Drill pattern incorrect","ScrapReasonID":4},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Drill size too large","ScrapReasonID":5},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Drill size too small","ScrapReasonID":6},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Handling damage","ScrapReasonID":7},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Paint process failed","ScrapReasonID":8},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Primer process failed","ScrapReasonID":9},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Seat assembly not as ordered","ScrapReasonID":10},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Stress test failed","ScrapReasonID":11},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Thermoform temperature too high","ScrapReasonID":12},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Thermoform temperature too low","ScrapReasonID":13},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Trim length too long","ScrapReasonID":14},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Trim length too short","ScrapReasonID":15},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Wheel misaligned","ScrapReasonID":16}
]';

ALTER TABLE [Production].[ScrapReason] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [Production].[ScrapReason] ON; 
MERGE INTO [Production].[ScrapReason] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ScrapReasonID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ScrapReasonID] SMALLINT
    )
) AS Source
ON Source.[ScrapReasonID] = Target.[ScrapReasonID]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
  INSERT (
        [ModifiedDate],
        [Name],
        [ScrapReasonID]
  ) VALUES (
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ScrapReasonID]  
  )
;
SET IDENTITY_INSERT [Production].[ScrapReason] OFF;
ALTER TABLE [Production].[ScrapReason] ENABLE TRIGGER ALL;
