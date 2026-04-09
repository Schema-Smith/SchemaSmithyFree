
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.category.tabledata}}';



MERGE INTO [dbo].[category] AS Target
USING (
  SELECT [category_id],[last_update],[name]
    FROM OPENJSON(@v_json)
    WITH (
           [category_id] INT,
           [last_update] DATETIME,
           [name] NVARCHAR(25)
    )
) AS Source
ON Source.[category_id] = Target.[category_id]

WHEN MATCHED AND (NOT (Target.[category_id] = Source.[category_id] OR (Target.[category_id] IS NULL AND Source.[category_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[name] = Source.[name] OR (Target.[name] IS NULL AND Source.[name] IS NULL))) THEN
  UPDATE SET
        [category_id] = Source.[category_id],
        [last_update] = Source.[last_update],
        [name] = Source.[name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [category_id],
        [last_update],
        [name]
   ) VALUES (
         Source.[category_id],
        Source.[last_update],
        Source.[name]
   )
 ;
