
DECLARE @v_json NVARCHAR(MAX) = '[
{"Database Version":"15.0.4280.7","ModifiedDate":"2023-05-08T12:07:29.843","SystemInformationID":1,"VersionDate":"2023-01-23T13:08:53.190"}
]';

ALTER TABLE [dbo].[AWBuildVersion] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [dbo].[AWBuildVersion] ON; 
MERGE INTO [dbo].[AWBuildVersion] AS Target
USING (
  SELECT [Database Version],[ModifiedDate],[SystemInformationID],[VersionDate]
    FROM OPENJSON(@v_json)
    WITH (
           [Database Version] NVARCHAR(25),
           [ModifiedDate] DATETIME,
           [SystemInformationID] TINYINT,
           [VersionDate] DATETIME
    )
) AS Source
ON Source.[SystemInformationID] = Target.[SystemInformationID]


WHEN MATCHED AND (NOT (Target.[Database Version] = Source.[Database Version] OR (Target.[Database Version] IS NULL AND Source.[Database Version] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[VersionDate] = Source.[VersionDate] OR (Target.[VersionDate] IS NULL AND Source.[VersionDate] IS NULL))) THEN
  UPDATE SET
        [Database Version] = Source.[Database Version],
        [ModifiedDate] = Source.[ModifiedDate],
        [VersionDate] = Source.[VersionDate]


WHEN NOT MATCHED THEN
  INSERT (
        [Database Version],
        [ModifiedDate],
        [SystemInformationID],
        [VersionDate]
  ) VALUES (
        Source.[Database Version],
        Source.[ModifiedDate],
        Source.[SystemInformationID],
        Source.[VersionDate]  
  )
;
SET IDENTITY_INSERT [dbo].[AWBuildVersion] OFF;
ALTER TABLE [dbo].[AWBuildVersion] ENABLE TRIGGER ALL;
