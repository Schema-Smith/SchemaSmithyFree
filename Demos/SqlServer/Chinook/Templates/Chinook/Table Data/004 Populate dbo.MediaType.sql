
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.MediaType.tabledata}}';


SET IDENTITY_INSERT [dbo].[MediaType] ON;
MERGE INTO [dbo].[MediaType] AS Target
USING (
  SELECT [MediaTypeId],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [MediaTypeId] INT,
           [Name] NVARCHAR(120)
    )
) AS Source
ON Source.[MediaTypeId] = Target.[MediaTypeId]

WHEN MATCHED AND (NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [MediaTypeId],
        [Name]
   ) VALUES (
         Source.[MediaTypeId],
        Source.[Name]
   )
 ;
SET IDENTITY_INSERT [dbo].[MediaType] OFF;
