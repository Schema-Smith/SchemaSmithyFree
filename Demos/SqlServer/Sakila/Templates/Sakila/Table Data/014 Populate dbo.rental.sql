
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.rental.tabledata}}';



MERGE INTO [dbo].[rental] AS Target
USING (
  SELECT [customer_id],[inventory_id],[last_update],[rental_date],[rental_id],[return_date],[staff_id]
    FROM OPENJSON(@v_json)
    WITH (
           [customer_id] INT,
           [inventory_id] INT,
           [last_update] DATETIME,
           [rental_date] DATETIME,
           [rental_id] INT,
           [return_date] DATETIME,
           [staff_id] INT
    )
) AS Source
ON Source.[rental_id] = Target.[rental_id]

WHEN MATCHED AND (NOT (Target.[customer_id] = Source.[customer_id] OR (Target.[customer_id] IS NULL AND Source.[customer_id] IS NULL)) OR NOT (Target.[inventory_id] = Source.[inventory_id] OR (Target.[inventory_id] IS NULL AND Source.[inventory_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[rental_date] = Source.[rental_date] OR (Target.[rental_date] IS NULL AND Source.[rental_date] IS NULL)) OR NOT (Target.[rental_id] = Source.[rental_id] OR (Target.[rental_id] IS NULL AND Source.[rental_id] IS NULL)) OR NOT (Target.[return_date] = Source.[return_date] OR (Target.[return_date] IS NULL AND Source.[return_date] IS NULL)) OR NOT (Target.[staff_id] = Source.[staff_id] OR (Target.[staff_id] IS NULL AND Source.[staff_id] IS NULL))) THEN
  UPDATE SET
        [customer_id] = Source.[customer_id],
        [inventory_id] = Source.[inventory_id],
        [last_update] = Source.[last_update],
        [rental_date] = Source.[rental_date],
        [rental_id] = Source.[rental_id],
        [return_date] = Source.[return_date],
        [staff_id] = Source.[staff_id]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [customer_id],
        [inventory_id],
        [last_update],
        [rental_date],
        [rental_id],
        [return_date],
        [staff_id]
   ) VALUES (
         Source.[customer_id],
        Source.[inventory_id],
        Source.[last_update],
        Source.[rental_date],
        Source.[rental_id],
        Source.[return_date],
        Source.[staff_id]
   )
 ;
