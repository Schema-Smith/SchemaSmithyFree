
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.language.tabledata}}';



MERGE INTO [dbo].[language] AS Target
USING (
  SELECT [language_id],[last_update],[name]
    FROM OPENJSON(@v_json)
    WITH (
           [language_id] INT,
           [last_update] DATETIME,
           [name] NCHAR(20)
    )
) AS Source
ON Source.[language_id] = Target.[language_id]

WHEN MATCHED AND (NOT (Target.[language_id] = Source.[language_id] OR (Target.[language_id] IS NULL AND Source.[language_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[name] = Source.[name] OR (Target.[name] IS NULL AND Source.[name] IS NULL))) THEN
  UPDATE SET
        [language_id] = Source.[language_id],
        [last_update] = Source.[last_update],
        [name] = Source.[name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [language_id],
        [last_update],
        [name]
   ) VALUES (
         Source.[language_id],
        Source.[last_update],
        Source.[name]
   )
 ;
