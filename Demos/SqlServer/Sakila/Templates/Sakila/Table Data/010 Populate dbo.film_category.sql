
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.film_category.tabledata}}';



MERGE INTO [dbo].[film_category] AS Target
USING (
  SELECT [category_id],[film_id],[last_update]
    FROM OPENJSON(@v_json)
    WITH (
           [category_id] INT,
           [film_id] INT,
           [last_update] DATETIME
    )
) AS Source
ON Source.[category_id] = Target.[category_id] AND Source.[film_id] = Target.[film_id]

WHEN MATCHED AND (NOT (Target.[category_id] = Source.[category_id] OR (Target.[category_id] IS NULL AND Source.[category_id] IS NULL)) OR NOT (Target.[film_id] = Source.[film_id] OR (Target.[film_id] IS NULL AND Source.[film_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL))) THEN
  UPDATE SET
        [category_id] = Source.[category_id],
        [film_id] = Source.[film_id],
        [last_update] = Source.[last_update]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [category_id],
        [film_id],
        [last_update]
   ) VALUES (
         Source.[category_id],
        Source.[film_id],
        Source.[last_update]
   )
 ;
