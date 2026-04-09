
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Genre.tabledata}}';


SET IDENTITY_INSERT [dbo].[Genre] ON;
MERGE INTO [dbo].[Genre] AS Target
USING (
  SELECT [GenreId],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [GenreId] INT,
           [Name] NVARCHAR(120)
    )
) AS Source
ON Source.[GenreId] = Target.[GenreId]

WHEN MATCHED AND (NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [GenreId],
        [Name]
   ) VALUES (
         Source.[GenreId],
        Source.[Name]
   )
 ;
SET IDENTITY_INSERT [dbo].[Genre] OFF;
