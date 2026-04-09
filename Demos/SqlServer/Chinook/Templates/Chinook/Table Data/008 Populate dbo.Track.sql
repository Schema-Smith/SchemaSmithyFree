
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Track.tabledata}}';


SET IDENTITY_INSERT [dbo].[Track] ON;
MERGE INTO [dbo].[Track] AS Target
USING (
  SELECT [AlbumId],[Bytes],[Composer],[GenreId],[MediaTypeId],[Milliseconds],[Name],[TrackId],[UnitPrice]
    FROM OPENJSON(@v_json)
    WITH (
           [AlbumId] INT,
           [Bytes] INT,
           [Composer] NVARCHAR(220),
           [GenreId] INT,
           [MediaTypeId] INT,
           [Milliseconds] INT,
           [Name] NVARCHAR(200),
           [TrackId] INT,
           [UnitPrice] NUMERIC(10, 2)
    )
) AS Source
ON Source.[TrackId] = Target.[TrackId]

WHEN MATCHED AND (NOT (Target.[AlbumId] = Source.[AlbumId] OR (Target.[AlbumId] IS NULL AND Source.[AlbumId] IS NULL)) OR NOT (Target.[Bytes] = Source.[Bytes] OR (Target.[Bytes] IS NULL AND Source.[Bytes] IS NULL)) OR NOT (Target.[Composer] = Source.[Composer] OR (Target.[Composer] IS NULL AND Source.[Composer] IS NULL)) OR NOT (Target.[GenreId] = Source.[GenreId] OR (Target.[GenreId] IS NULL AND Source.[GenreId] IS NULL)) OR NOT (Target.[MediaTypeId] = Source.[MediaTypeId] OR (Target.[MediaTypeId] IS NULL AND Source.[MediaTypeId] IS NULL)) OR NOT (Target.[Milliseconds] = Source.[Milliseconds] OR (Target.[Milliseconds] IS NULL AND Source.[Milliseconds] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[UnitPrice] = Source.[UnitPrice] OR (Target.[UnitPrice] IS NULL AND Source.[UnitPrice] IS NULL))) THEN
  UPDATE SET
        [AlbumId] = Source.[AlbumId],
        [Bytes] = Source.[Bytes],
        [Composer] = Source.[Composer],
        [GenreId] = Source.[GenreId],
        [MediaTypeId] = Source.[MediaTypeId],
        [Milliseconds] = Source.[Milliseconds],
        [Name] = Source.[Name],
        [UnitPrice] = Source.[UnitPrice]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AlbumId],
        [Bytes],
        [Composer],
        [GenreId],
        [MediaTypeId],
        [Milliseconds],
        [Name],
        [TrackId],
        [UnitPrice]
   ) VALUES (
         Source.[AlbumId],
        Source.[Bytes],
        Source.[Composer],
        Source.[GenreId],
        Source.[MediaTypeId],
        Source.[Milliseconds],
        Source.[Name],
        Source.[TrackId],
        Source.[UnitPrice]
   )
 ;
SET IDENTITY_INSERT [dbo].[Track] OFF;
