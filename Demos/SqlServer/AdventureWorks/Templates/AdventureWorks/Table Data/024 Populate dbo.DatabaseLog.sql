
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.DatabaseLog.tabledata}}';


SET IDENTITY_INSERT [dbo].[DatabaseLog] ON;
MERGE INTO [dbo].[DatabaseLog] AS Target
USING (
  SELECT [DatabaseLogID],[DatabaseUser],[Event],[Object],[PostTime],[Schema],[TSQL],[XmlEvent]
    FROM OPENJSON(@v_json)
    WITH (
           [DatabaseLogID] INT,
           [DatabaseUser] SYSNAME,
           [Event] SYSNAME,
           [Object] SYSNAME,
           [PostTime] DATETIME,
           [Schema] SYSNAME,
           [TSQL] NVARCHAR(MAX),
           [XmlEvent] XML
    )
) AS Source
ON Source.[DatabaseLogID] = Target.[DatabaseLogID]

WHEN MATCHED AND (NOT (Target.[DatabaseUser] = Source.[DatabaseUser] OR (Target.[DatabaseUser] IS NULL AND Source.[DatabaseUser] IS NULL)) OR NOT (Target.[Event] = Source.[Event] OR (Target.[Event] IS NULL AND Source.[Event] IS NULL)) OR NOT (Target.[Object] = Source.[Object] OR (Target.[Object] IS NULL AND Source.[Object] IS NULL)) OR NOT (Target.[PostTime] = Source.[PostTime] OR (Target.[PostTime] IS NULL AND Source.[PostTime] IS NULL)) OR NOT (Target.[Schema] = Source.[Schema] OR (Target.[Schema] IS NULL AND Source.[Schema] IS NULL)) OR NOT (Target.[TSQL] = Source.[TSQL] OR (Target.[TSQL] IS NULL AND Source.[TSQL] IS NULL)) OR NOT (CAST(Target.[XmlEvent] AS NVARCHAR(MAX)) = CAST(Source.[XmlEvent] AS NVARCHAR(MAX)) OR (Target.[XmlEvent] IS NULL AND Source.[XmlEvent] IS NULL))) THEN
  UPDATE SET
        [DatabaseUser] = Source.[DatabaseUser],
        [Event] = Source.[Event],
        [Object] = Source.[Object],
        [PostTime] = Source.[PostTime],
        [Schema] = Source.[Schema],
        [TSQL] = Source.[TSQL],
        [XmlEvent] = Source.[XmlEvent]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [DatabaseLogID],
        [DatabaseUser],
        [Event],
        [Object],
        [PostTime],
        [Schema],
        [TSQL],
        [XmlEvent]
   ) VALUES (
         Source.[DatabaseLogID],
        Source.[DatabaseUser],
        Source.[Event],
        Source.[Object],
        Source.[PostTime],
        Source.[Schema],
        Source.[TSQL],
        Source.[XmlEvent]
   )
 ;
SET IDENTITY_INSERT [dbo].[DatabaseLog] OFF;
