
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.PlaylistTrack.tabledata}}';



MERGE INTO [dbo].[PlaylistTrack] AS Target
USING (
  SELECT [PlaylistId],[TrackId]
    FROM OPENJSON(@v_json)
    WITH (
           [PlaylistId] INT,
           [TrackId] INT
    )
) AS Source
ON Source.[PlaylistId] = Target.[PlaylistId] AND Source.[TrackId] = Target.[TrackId]

WHEN MATCHED AND (NOT (Target.[PlaylistId] = Source.[PlaylistId] OR (Target.[PlaylistId] IS NULL AND Source.[PlaylistId] IS NULL)) OR NOT (Target.[TrackId] = Source.[TrackId] OR (Target.[TrackId] IS NULL AND Source.[TrackId] IS NULL))) THEN
  UPDATE SET
        [PlaylistId] = Source.[PlaylistId],
        [TrackId] = Source.[TrackId]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [PlaylistId],
        [TrackId]
   ) VALUES (
         Source.[PlaylistId],
        Source.[TrackId]
   )
 ;
