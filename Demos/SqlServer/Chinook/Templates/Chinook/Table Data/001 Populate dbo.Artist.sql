
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Artist.tabledata}}';


SET IDENTITY_INSERT [dbo].[Artist] ON;
MERGE INTO [dbo].[Artist] AS Target
USING (
  SELECT [ArtistId],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [ArtistId] INT,
           [Name] NVARCHAR(120)
    )
) AS Source
ON Source.[ArtistId] = Target.[ArtistId]

WHEN MATCHED AND (NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ArtistId],
        [Name]
   ) VALUES (
         Source.[ArtistId],
        Source.[Name]
   )
 ;
SET IDENTITY_INSERT [dbo].[Artist] OFF;
