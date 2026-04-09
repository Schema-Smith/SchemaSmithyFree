
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.address.tabledata}}';



MERGE INTO [dbo].[address] AS Target
USING (
  SELECT [address],[address_id],[address2],[city_id],[district],[last_update],[phone],[postal_code]
    FROM OPENJSON(@v_json)
    WITH (
           [address] NVARCHAR(50),
           [address_id] INT,
           [address2] NVARCHAR(50),
           [city_id] INT,
           [district] NVARCHAR(20),
           [last_update] DATETIME,
           [phone] NVARCHAR(20),
           [postal_code] NVARCHAR(10)
    )
) AS Source
ON Source.[address_id] = Target.[address_id]

WHEN MATCHED AND (NOT (Target.[address] = Source.[address] OR (Target.[address] IS NULL AND Source.[address] IS NULL)) OR NOT (Target.[address_id] = Source.[address_id] OR (Target.[address_id] IS NULL AND Source.[address_id] IS NULL)) OR NOT (Target.[address2] = Source.[address2] OR (Target.[address2] IS NULL AND Source.[address2] IS NULL)) OR NOT (Target.[city_id] = Source.[city_id] OR (Target.[city_id] IS NULL AND Source.[city_id] IS NULL)) OR NOT (Target.[district] = Source.[district] OR (Target.[district] IS NULL AND Source.[district] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[phone] = Source.[phone] OR (Target.[phone] IS NULL AND Source.[phone] IS NULL)) OR NOT (Target.[postal_code] = Source.[postal_code] OR (Target.[postal_code] IS NULL AND Source.[postal_code] IS NULL))) THEN
  UPDATE SET
        [address] = Source.[address],
        [address_id] = Source.[address_id],
        [address2] = Source.[address2],
        [city_id] = Source.[city_id],
        [district] = Source.[district],
        [last_update] = Source.[last_update],
        [phone] = Source.[phone],
        [postal_code] = Source.[postal_code]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [address],
        [address_id],
        [address2],
        [city_id],
        [district],
        [last_update],
        [phone],
        [postal_code]
   ) VALUES (
         Source.[address],
        Source.[address_id],
        Source.[address2],
        Source.[city_id],
        Source.[district],
        Source.[last_update],
        Source.[phone],
        Source.[postal_code]
   )
 ;
