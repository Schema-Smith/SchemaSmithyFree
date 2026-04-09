
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.country.tabledata}}';



MERGE INTO [dbo].[country] AS Target
USING (
  SELECT [country],[country_id],[last_update]
    FROM OPENJSON(@v_json)
    WITH (
           [country] NVARCHAR(50),
           [country_id] INT,
           [last_update] DATETIME
    )
) AS Source
ON Source.[country_id] = Target.[country_id]

WHEN MATCHED AND (NOT (Target.[country] = Source.[country] OR (Target.[country] IS NULL AND Source.[country] IS NULL)) OR NOT (Target.[country_id] = Source.[country_id] OR (Target.[country_id] IS NULL AND Source.[country_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL))) THEN
  UPDATE SET
        [country] = Source.[country],
        [country_id] = Source.[country_id],
        [last_update] = Source.[last_update]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [country],
        [country_id],
        [last_update]
   ) VALUES (
         Source.[country],
        Source.[country_id],
        Source.[last_update]
   )
 ;
