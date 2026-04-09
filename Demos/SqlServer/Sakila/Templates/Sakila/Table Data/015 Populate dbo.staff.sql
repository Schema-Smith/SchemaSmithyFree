
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.staff.tabledata}}';



MERGE INTO [dbo].[staff] AS Target
USING (
  SELECT [active],[address_id],[email],[first_name],[last_name],[last_update],[password],[picture],[staff_id],[store_id],[username]
    FROM OPENJSON(@v_json)
    WITH (
           [active] TINYINT,
           [address_id] INT,
           [email] NVARCHAR(50),
           [first_name] NVARCHAR(45),
           [last_name] NVARCHAR(45),
           [last_update] DATETIME,
           [password] NVARCHAR(40),
           [picture] VARBINARY(MAX),
           [staff_id] INT,
           [store_id] INT,
           [username] NVARCHAR(16)
    )
) AS Source
ON Source.[staff_id] = Target.[staff_id]

WHEN MATCHED AND (NOT (Target.[active] = Source.[active] OR (Target.[active] IS NULL AND Source.[active] IS NULL)) OR NOT (Target.[address_id] = Source.[address_id] OR (Target.[address_id] IS NULL AND Source.[address_id] IS NULL)) OR NOT (Target.[email] = Source.[email] OR (Target.[email] IS NULL AND Source.[email] IS NULL)) OR NOT (Target.[first_name] = Source.[first_name] OR (Target.[first_name] IS NULL AND Source.[first_name] IS NULL)) OR NOT (Target.[last_name] = Source.[last_name] OR (Target.[last_name] IS NULL AND Source.[last_name] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[password] = Source.[password] OR (Target.[password] IS NULL AND Source.[password] IS NULL)) OR NOT (Target.[picture] = Source.[picture] OR (Target.[picture] IS NULL AND Source.[picture] IS NULL)) OR NOT (Target.[staff_id] = Source.[staff_id] OR (Target.[staff_id] IS NULL AND Source.[staff_id] IS NULL)) OR NOT (Target.[store_id] = Source.[store_id] OR (Target.[store_id] IS NULL AND Source.[store_id] IS NULL)) OR NOT (Target.[username] = Source.[username] OR (Target.[username] IS NULL AND Source.[username] IS NULL))) THEN
  UPDATE SET
        [active] = Source.[active],
        [address_id] = Source.[address_id],
        [email] = Source.[email],
        [first_name] = Source.[first_name],
        [last_name] = Source.[last_name],
        [last_update] = Source.[last_update],
        [password] = Source.[password],
        [picture] = Source.[picture],
        [staff_id] = Source.[staff_id],
        [store_id] = Source.[store_id],
        [username] = Source.[username]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [active],
        [address_id],
        [email],
        [first_name],
        [last_name],
        [last_update],
        [password],
        [picture],
        [staff_id],
        [store_id],
        [username]
   ) VALUES (
         Source.[active],
        Source.[address_id],
        Source.[email],
        Source.[first_name],
        Source.[last_name],
        Source.[last_update],
        Source.[password],
        Source.[picture],
        Source.[staff_id],
        Source.[store_id],
        Source.[username]
   )
 ;
