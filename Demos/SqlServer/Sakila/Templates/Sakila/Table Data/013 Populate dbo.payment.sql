
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.payment.tabledata}}';



MERGE INTO [dbo].[payment] AS Target
USING (
  SELECT [amount],[customer_id],[last_update],[payment_date],[payment_id],[rental_id],[staff_id]
    FROM OPENJSON(@v_json)
    WITH (
           [amount] DECIMAL(5, 2),
           [customer_id] INT,
           [last_update] DATETIME,
           [payment_date] DATETIME,
           [payment_id] INT,
           [rental_id] INT,
           [staff_id] INT
    )
) AS Source
ON Source.[payment_id] = Target.[payment_id]

WHEN MATCHED AND (NOT (Target.[amount] = Source.[amount] OR (Target.[amount] IS NULL AND Source.[amount] IS NULL)) OR NOT (Target.[customer_id] = Source.[customer_id] OR (Target.[customer_id] IS NULL AND Source.[customer_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[payment_date] = Source.[payment_date] OR (Target.[payment_date] IS NULL AND Source.[payment_date] IS NULL)) OR NOT (Target.[payment_id] = Source.[payment_id] OR (Target.[payment_id] IS NULL AND Source.[payment_id] IS NULL)) OR NOT (Target.[rental_id] = Source.[rental_id] OR (Target.[rental_id] IS NULL AND Source.[rental_id] IS NULL)) OR NOT (Target.[staff_id] = Source.[staff_id] OR (Target.[staff_id] IS NULL AND Source.[staff_id] IS NULL))) THEN
  UPDATE SET
        [amount] = Source.[amount],
        [customer_id] = Source.[customer_id],
        [last_update] = Source.[last_update],
        [payment_date] = Source.[payment_date],
        [payment_id] = Source.[payment_id],
        [rental_id] = Source.[rental_id],
        [staff_id] = Source.[staff_id]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [amount],
        [customer_id],
        [last_update],
        [payment_date],
        [payment_id],
        [rental_id],
        [staff_id]
   ) VALUES (
         Source.[amount],
        Source.[customer_id],
        Source.[last_update],
        Source.[payment_date],
        Source.[payment_id],
        Source.[rental_id],
        Source.[staff_id]
   )
 ;
