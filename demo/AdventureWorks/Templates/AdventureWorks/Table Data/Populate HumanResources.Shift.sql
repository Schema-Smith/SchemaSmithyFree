
DECLARE @v_json NVARCHAR(MAX) = '[
{"EndTime":"15:00:00","ModifiedDate":"2008-04-30T00:00:00","Name":"Day","ShiftID":1,"StartTime":"07:00:00"},
{"EndTime":"23:00:00","ModifiedDate":"2008-04-30T00:00:00","Name":"Evening","ShiftID":2,"StartTime":"15:00:00"},
{"EndTime":"07:00:00","ModifiedDate":"2008-04-30T00:00:00","Name":"Night","ShiftID":3,"StartTime":"23:00:00"}
]';

ALTER TABLE [HumanResources].[Shift] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [HumanResources].[Shift] ON; 
MERGE INTO [HumanResources].[Shift] AS Target
USING (
  SELECT [EndTime],[ModifiedDate],[Name],[ShiftID],[StartTime]
    FROM OPENJSON(@v_json)
    WITH (
           [EndTime] TIME,
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ShiftID] TINYINT,
           [StartTime] TIME
    )
) AS Source
ON Source.[ShiftID] = Target.[ShiftID]


WHEN MATCHED AND (NOT (Target.[EndTime] = Source.[EndTime] OR (Target.[EndTime] IS NULL AND Source.[EndTime] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[StartTime] = Source.[StartTime] OR (Target.[StartTime] IS NULL AND Source.[StartTime] IS NULL))) THEN
  UPDATE SET
        [EndTime] = Source.[EndTime],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [StartTime] = Source.[StartTime]


WHEN NOT MATCHED THEN
  INSERT (
        [EndTime],
        [ModifiedDate],
        [Name],
        [ShiftID],
        [StartTime]
  ) VALUES (
        Source.[EndTime],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ShiftID],
        Source.[StartTime]  
  )
;
SET IDENTITY_INSERT [HumanResources].[Shift] OFF;
ALTER TABLE [HumanResources].[Shift] ENABLE TRIGGER ALL;
