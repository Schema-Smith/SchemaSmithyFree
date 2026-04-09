
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.film.tabledata}}';



MERGE INTO [dbo].[film] AS Target
USING (
  SELECT [description],[film_id],[language_id],[last_update],[length],[original_language_id],[rating],[release_year],[rental_duration],[rental_rate],[replacement_cost],[special_features],[title]
    FROM OPENJSON(@v_json)
    WITH (
           [description] NVARCHAR(MAX),
           [film_id] INT,
           [language_id] INT,
           [last_update] DATETIME,
           [length] SMALLINT,
           [original_language_id] INT,
           [rating] NVARCHAR(10),
           [release_year] SMALLINT,
           [rental_duration] TINYINT,
           [rental_rate] DECIMAL(4, 2),
           [replacement_cost] DECIMAL(5, 2),
           [special_features] NVARCHAR(255),
           [title] NVARCHAR(255)
    )
) AS Source
ON Source.[film_id] = Target.[film_id]

WHEN MATCHED AND (NOT (Target.[description] = Source.[description] OR (Target.[description] IS NULL AND Source.[description] IS NULL)) OR NOT (Target.[film_id] = Source.[film_id] OR (Target.[film_id] IS NULL AND Source.[film_id] IS NULL)) OR NOT (Target.[language_id] = Source.[language_id] OR (Target.[language_id] IS NULL AND Source.[language_id] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL)) OR NOT (Target.[length] = Source.[length] OR (Target.[length] IS NULL AND Source.[length] IS NULL)) OR NOT (Target.[original_language_id] = Source.[original_language_id] OR (Target.[original_language_id] IS NULL AND Source.[original_language_id] IS NULL)) OR NOT (Target.[rating] = Source.[rating] OR (Target.[rating] IS NULL AND Source.[rating] IS NULL)) OR NOT (Target.[release_year] = Source.[release_year] OR (Target.[release_year] IS NULL AND Source.[release_year] IS NULL)) OR NOT (Target.[rental_duration] = Source.[rental_duration] OR (Target.[rental_duration] IS NULL AND Source.[rental_duration] IS NULL)) OR NOT (Target.[rental_rate] = Source.[rental_rate] OR (Target.[rental_rate] IS NULL AND Source.[rental_rate] IS NULL)) OR NOT (Target.[replacement_cost] = Source.[replacement_cost] OR (Target.[replacement_cost] IS NULL AND Source.[replacement_cost] IS NULL)) OR NOT (Target.[special_features] = Source.[special_features] OR (Target.[special_features] IS NULL AND Source.[special_features] IS NULL)) OR NOT (Target.[title] = Source.[title] OR (Target.[title] IS NULL AND Source.[title] IS NULL))) THEN
  UPDATE SET
        [description] = Source.[description],
        [film_id] = Source.[film_id],
        [language_id] = Source.[language_id],
        [last_update] = Source.[last_update],
        [length] = Source.[length],
        [original_language_id] = Source.[original_language_id],
        [rating] = Source.[rating],
        [release_year] = Source.[release_year],
        [rental_duration] = Source.[rental_duration],
        [rental_rate] = Source.[rental_rate],
        [replacement_cost] = Source.[replacement_cost],
        [special_features] = Source.[special_features],
        [title] = Source.[title]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [description],
        [film_id],
        [language_id],
        [last_update],
        [length],
        [original_language_id],
        [rating],
        [release_year],
        [rental_duration],
        [rental_rate],
        [replacement_cost],
        [special_features],
        [title]
   ) VALUES (
         Source.[description],
        Source.[film_id],
        Source.[language_id],
        Source.[last_update],
        Source.[length],
        Source.[original_language_id],
        Source.[rating],
        Source.[release_year],
        Source.[rental_duration],
        Source.[rental_rate],
        Source.[replacement_cost],
        Source.[special_features],
        Source.[title]
   )
 ;
