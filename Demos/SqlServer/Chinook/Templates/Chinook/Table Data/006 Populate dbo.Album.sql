
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Album.tabledata}}';


SET IDENTITY_INSERT [dbo].[Album] ON;
MERGE INTO [dbo].[Album] AS Target
USING (
  SELECT [AlbumId],[ArtistId],[Title]
    FROM OPENJSON(@v_json)
    WITH (
           [AlbumId] INT,
           [ArtistId] INT,
           [Title] NVARCHAR(160)
    )
) AS Source
ON Source.[AlbumId] = Target.[AlbumId]

WHEN MATCHED AND (NOT (Target.[ArtistId] = Source.[ArtistId] OR (Target.[ArtistId] IS NULL AND Source.[ArtistId] IS NULL)) OR NOT (Target.[Title] = Source.[Title] OR (Target.[Title] IS NULL AND Source.[Title] IS NULL))) THEN
  UPDATE SET
        [ArtistId] = Source.[ArtistId],
        [Title] = Source.[Title]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AlbumId],
        [ArtistId],
        [Title]
   ) VALUES (
         Source.[AlbumId],
        Source.[ArtistId],
        Source.[Title]
   )
 ;
SET IDENTITY_INSERT [dbo].[Album] OFF;
