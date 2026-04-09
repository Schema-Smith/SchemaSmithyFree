
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductModelIllustration.tabledata}}';



MERGE INTO [Production].[ProductModelIllustration] AS Target
USING (
  SELECT [IllustrationID],[ModifiedDate],[ProductModelID]
    FROM OPENJSON(@v_json)
    WITH (
           [IllustrationID] INT,
           [ModifiedDate] DATETIME,
           [ProductModelID] INT
    )
) AS Source
ON Source.[IllustrationID] = Target.[IllustrationID] AND Source.[ProductModelID] = Target.[ProductModelID]

WHEN MATCHED AND (NOT (Target.[IllustrationID] = Source.[IllustrationID] OR (Target.[IllustrationID] IS NULL AND Source.[IllustrationID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductModelID] = Source.[ProductModelID] OR (Target.[ProductModelID] IS NULL AND Source.[ProductModelID] IS NULL))) THEN
  UPDATE SET
        [IllustrationID] = Source.[IllustrationID],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductModelID] = Source.[ProductModelID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [IllustrationID],
        [ModifiedDate],
        [ProductModelID]
   ) VALUES (
         Source.[IllustrationID],
        Source.[ModifiedDate],
        Source.[ProductModelID]
   )
 ;
