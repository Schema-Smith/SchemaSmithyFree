
DECLARE @v_json NVARCHAR(MAX) = '{{HumanResources.Shift.tabledata}}';


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

WHEN MATCHED AND (NOT (Target.[EndTime] = Source.[EndTime] OR (Target.[EndTime] IS NULL AND Source.[EndTime] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[StartTime] = Source.[StartTime] OR (Target.[StartTime] IS NULL AND Source.[StartTime] IS NULL))) THEN
  UPDATE SET
        [EndTime] = Source.[EndTime],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [StartTime] = Source.[StartTime]

 WHEN NOT MATCHED BY TARGET THEN
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
