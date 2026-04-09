
DECLARE @v_json NVARCHAR(MAX) = '{{Production.Culture.tabledata}}';



MERGE INTO [Production].[Culture] AS Target
USING (
  SELECT [CultureID],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [CultureID] NCHAR(6),
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[CultureID] = Target.[CultureID]

WHEN MATCHED AND (NOT (Target.[CultureID] = Source.[CultureID] OR (Target.[CultureID] IS NULL AND Source.[CultureID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [CultureID] = Source.[CultureID],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CultureID],
        [ModifiedDate],
        [Name]
   ) VALUES (
         Source.[CultureID],
        Source.[ModifiedDate],
        Source.[Name]
   )
 ;
