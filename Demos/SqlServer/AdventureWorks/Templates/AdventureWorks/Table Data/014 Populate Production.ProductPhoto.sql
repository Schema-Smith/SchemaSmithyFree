
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductPhoto.tabledata}}';


SET IDENTITY_INSERT [Production].[ProductPhoto] ON;
MERGE INTO [Production].[ProductPhoto] AS Target
USING (
  SELECT [LargePhoto],[LargePhotoFileName],[ModifiedDate],[ProductPhotoID],[ThumbNailPhoto],[ThumbnailPhotoFileName]
    FROM OPENJSON(@v_json)
    WITH (
           [LargePhoto] VARBINARY(MAX),
           [LargePhotoFileName] NVARCHAR(50),
           [ModifiedDate] DATETIME,
           [ProductPhotoID] INT,
           [ThumbNailPhoto] VARBINARY(MAX),
           [ThumbnailPhotoFileName] NVARCHAR(50)
    )
) AS Source
ON Source.[ProductPhotoID] = Target.[ProductPhotoID]

WHEN MATCHED AND (NOT (Target.[LargePhoto] = Source.[LargePhoto] OR (Target.[LargePhoto] IS NULL AND Source.[LargePhoto] IS NULL)) OR NOT (Target.[LargePhotoFileName] = Source.[LargePhotoFileName] OR (Target.[LargePhotoFileName] IS NULL AND Source.[LargePhotoFileName] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ThumbNailPhoto] = Source.[ThumbNailPhoto] OR (Target.[ThumbNailPhoto] IS NULL AND Source.[ThumbNailPhoto] IS NULL)) OR NOT (Target.[ThumbnailPhotoFileName] = Source.[ThumbnailPhotoFileName] OR (Target.[ThumbnailPhotoFileName] IS NULL AND Source.[ThumbnailPhotoFileName] IS NULL))) THEN
  UPDATE SET
        [LargePhoto] = Source.[LargePhoto],
        [LargePhotoFileName] = Source.[LargePhotoFileName],
        [ModifiedDate] = Source.[ModifiedDate],
        [ThumbNailPhoto] = Source.[ThumbNailPhoto],
        [ThumbnailPhotoFileName] = Source.[ThumbnailPhotoFileName]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [LargePhoto],
        [LargePhotoFileName],
        [ModifiedDate],
        [ProductPhotoID],
        [ThumbNailPhoto],
        [ThumbnailPhotoFileName]
   ) VALUES (
         Source.[LargePhoto],
        Source.[LargePhotoFileName],
        Source.[ModifiedDate],
        Source.[ProductPhotoID],
        Source.[ThumbNailPhoto],
        Source.[ThumbnailPhotoFileName]
   )
 ;
SET IDENTITY_INSERT [Production].[ProductPhoto] OFF;
