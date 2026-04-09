
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.actor.tabledata}}';



MERGE INTO [dbo].[actor] AS Target
USING (
  SELECT [actor_id],[first_name],[last_name],[last_update]
    FROM OPENJSON(@v_json)
    WITH (
           [actor_id] INT,
           [first_name] NVARCHAR(45),
           [last_name] NVARCHAR(45),
           [last_update] DATETIME
    )
) AS Source
ON Source.[actor_id] = Target.[actor_id]

WHEN MATCHED AND (NOT (Target.[actor_id] = Source.[actor_id] OR (Target.[actor_id] IS NULL AND Source.[actor_id] IS NULL)) OR NOT (Target.[first_name] = Source.[first_name] OR (Target.[first_name] IS NULL AND Source.[first_name] IS NULL)) OR NOT (Target.[last_name] = Source.[last_name] OR (Target.[last_name] IS NULL AND Source.[last_name] IS NULL)) OR NOT (Target.[last_update] = Source.[last_update] OR (Target.[last_update] IS NULL AND Source.[last_update] IS NULL))) THEN
  UPDATE SET
        [actor_id] = Source.[actor_id],
        [first_name] = Source.[first_name],
        [last_name] = Source.[last_name],
        [last_update] = Source.[last_update]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [actor_id],
        [first_name],
        [last_name],
        [last_update]
   ) VALUES (
         Source.[actor_id],
        Source.[first_name],
        Source.[last_name],
        Source.[last_update]
   )
 ;
