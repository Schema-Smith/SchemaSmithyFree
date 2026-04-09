
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.customer.tabledata}}';



MERGE INTO [dbo].[customer] AS Target
USING (
  SELECT [active],[address_id],[create_date],[customer_id],[email],[first_name],[last_name],[last_update],[store_id]
    FROM OPENJSON(@v_json)
    WITH (
           [active] TINYINT,
           [address_id] INT,
           [create_date] DATETIME,
           [customer_id] INT,
           [email] NVARCHAR(50),
           [first_name] NVARCHAR(45),
           [last_name] NVARCHAR(45),
           [last_update] DATETIME,
           [store_id] INT
    )
) AS Source
ON Source.[customer_id] = Target.[customer_id]

WHEN MATCHED AND (NOT (Target.[active] = Source.[active] OR (Target.[active] IS NULL AND Source.[active] IS NULL)) OR NOT (Target.[address_id] = Source.[address_id] OR (Target.[address_id] IS NULL AND Source.[address_id] IS NULL)) OR NOT (Target.[create_date] = Source.[create_date] OR (Target.[create_date] IS NULL AND Source.[create_date] IS NULL)) OR NOT (Target.[customer_id] = Source.[customer_id] OR (Target.[customer_id] IS NULL AND Source.[customer_id] IS NULL)) OR NOT (Target.[email] = Source.[email] OR (Target.[email] IS NULL AND Source.[email] IS NULL)) OR NOT (Target.[first_name] = Source.[first_name] OR (Target.[first_name] IS NULL AND Source.[first_name] IS NULL)) OR NOT (Target.[last_name] = Source.[last_name] OR (Target.[last_name] IS NULL AND Source.[last_name] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[store_id] = Source.[store_id] OR (Target.[store_id] IS NULL AND Source.[store_id] IS NULL))) THEN
  UPDATE SET
        [active] = Source.[active],
        [address_id] = Source.[address_id],
        [create_date] = Source.[create_date],
        [customer_id] = Source.[customer_id],
        [email] = Source.[email],
        [first_name] = Source.[first_name],
        [last_name] = Source.[last_name],
        [last_update] = Source.[last_update],
        [store_id] = Source.[store_id]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [active],
        [address_id],
        [create_date],
        [customer_id],
        [email],
        [first_name],
        [last_name],
        [last_update],
        [store_id]
   ) VALUES (
         Source.[active],
        Source.[address_id],
        Source.[create_date],
        Source.[customer_id],
        Source.[email],
        Source.[first_name],
        Source.[last_name],
        Source.[last_update],
        Source.[store_id]
   )
 ;
