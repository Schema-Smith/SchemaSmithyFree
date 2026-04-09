
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.city.tabledata}}';



MERGE INTO [dbo].[city] AS Target
USING (
  SELECT [city],[city_id],[country_id],[last_update]
    FROM OPENJSON(@v_json)
    WITH (
           [city] NVARCHAR(50),
           [city_id] INT,
           [country_id] INT,
           [last_update] DATETIME
    )
) AS Source
ON Source.[city_id] = Target.[city_id]

WHEN MATCHED AND (NOT (Target.[city] = Source.[city] OR (Target.[city] IS NULL AND Source.[city] IS NULL)) OR NOT (Target.[city_id] = Source.[city_id] OR (Target.[city_id] IS NULL AND Source.[city_id] IS NULL)) OR NOT (Target.[country_id] = Source.[country_id] OR (Target.[country_id] IS NULL AND Source.[country_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL))) THEN
  UPDATE SET
        [city] = Source.[city],
        [city_id] = Source.[city_id],
        [country_id] = Source.[country_id],
        [last_update] = Source.[last_update]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [city],
        [city_id],
        [country_id],
        [last_update]
   ) VALUES (
         Source.[city],
        Source.[city_id],
        Source.[country_id],
        Source.[last_update]
   )
 ;
