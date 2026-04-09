
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.store.tabledata}}';



MERGE INTO [dbo].[store] AS Target
USING (
  SELECT [address_id],[last_update],[manager_staff_id],[store_id]
    FROM OPENJSON(@v_json)
    WITH (
           [address_id] INT,
           [last_update] DATETIME,
           [manager_staff_id] INT,
           [store_id] INT
    )
) AS Source
ON Source.[store_id] = Target.[store_id]

WHEN MATCHED AND (NOT (Target.[address_id] = Source.[address_id] OR (Target.[address_id] IS NULL AND Source.[address_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[manager_staff_id] = Source.[manager_staff_id] OR (Target.[manager_staff_id] IS NULL AND Source.[manager_staff_id] IS NULL)) OR NOT (Target.[store_id] = Source.[store_id] OR (Target.[store_id] IS NULL AND Source.[store_id] IS NULL))) THEN
  UPDATE SET
        [address_id] = Source.[address_id],
        [last_update] = Source.[last_update],
        [manager_staff_id] = Source.[manager_staff_id],
        [store_id] = Source.[store_id]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [address_id],
        [last_update],
        [manager_staff_id],
        [store_id]
   ) VALUES (
         Source.[address_id],
        Source.[last_update],
        Source.[manager_staff_id],
        Source.[store_id]
   )
 ;
