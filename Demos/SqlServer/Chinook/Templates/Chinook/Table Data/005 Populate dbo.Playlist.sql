
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Playlist.tabledata}}';


SET IDENTITY_INSERT [dbo].[Playlist] ON;
MERGE INTO [dbo].[Playlist] AS Target
USING (
  SELECT [Name],[PlaylistId]
    FROM OPENJSON(@v_json)
    WITH (
           [Name] NVARCHAR(120),
           [PlaylistId] INT
    )
) AS Source
ON Source.[PlaylistId] = Target.[PlaylistId]

WHEN MATCHED AND (NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Name],
        [PlaylistId]
   ) VALUES (
         Source.[Name],
        Source.[PlaylistId]
   )
 ;
SET IDENTITY_INSERT [dbo].[Playlist] OFF;
