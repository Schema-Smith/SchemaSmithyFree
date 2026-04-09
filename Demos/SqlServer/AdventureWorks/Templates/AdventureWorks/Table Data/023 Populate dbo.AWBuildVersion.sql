
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.AWBuildVersion.tabledata}}';


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

WHEN MATCHED AND (NOT (Target.[Database Version] = Source.[Database Version] OR (Target.[Database Version] IS NULL AND Source.[Database Version] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[VersionDate] = Source.[VersionDate] OR (Target.[VersionDate] IS NULL AND Source.[VersionDate] IS NULL))) THEN
  UPDATE SET
        [Database Version] = Source.[Database Version],
        [ModifiedDate] = Source.[ModifiedDate],
        [VersionDate] = Source.[VersionDate]

 WHEN NOT MATCHED BY TARGET THEN
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
