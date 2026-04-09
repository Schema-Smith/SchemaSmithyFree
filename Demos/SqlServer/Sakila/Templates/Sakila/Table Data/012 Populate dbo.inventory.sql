
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.inventory.tabledata}}';



MERGE INTO [dbo].[inventory] AS Target
USING (
  SELECT [film_id],[inventory_id],[last_update],[store_id]
    FROM OPENJSON(@v_json)
    WITH (
           [film_id] INT,
           [inventory_id] INT,
           [last_update] DATETIME,
           [store_id] INT
    )
) AS Source
ON Source.[inventory_id] = Target.[inventory_id]

WHEN MATCHED AND (NOT (Target.[film_id] = Source.[film_id] OR (Target.[film_id] IS NULL AND Source.[film_id] IS NULL)) OR NOT (Target.[inventory_id] = Source.[inventory_id] OR (Target.[inventory_id] IS NULL AND Source.[inventory_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[store_id] = Source.[store_id] OR (Target.[store_id] IS NULL AND Source.[store_id] IS NULL))) THEN
  UPDATE SET
        [film_id] = Source.[film_id],
        [inventory_id] = Source.[inventory_id],
        [last_update] = Source.[last_update],
        [store_id] = Source.[store_id]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [film_id],
        [inventory_id],
        [last_update],
        [store_id]
   ) VALUES (
         Source.[film_id],
        Source.[inventory_id],
        Source.[last_update],
        Source.[store_id]
   )
 ;
