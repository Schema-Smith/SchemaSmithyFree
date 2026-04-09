
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductDocument.tabledata}}';



MERGE INTO [Production].[ProductDocument] AS Target
USING (
  SELECT [DocumentNode],[ModifiedDate],[ProductID]
    FROM OPENJSON(@v_json)
    WITH (
           [DocumentNode] NVARCHAR(4000),
           [ModifiedDate] DATETIME,
           [ProductID] INT
    )
) AS Source
ON Source.[DocumentNode] = Target.[DocumentNode] AND Source.[ProductID] = Target.[ProductID]

WHEN MATCHED AND (NOT (Target.[DocumentNode] = Source.[DocumentNode] OR (Target.[DocumentNode] IS NULL AND Source.[DocumentNode] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL))) THEN
  UPDATE SET
        [DocumentNode] = Source.[DocumentNode],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [DocumentNode],
        [ModifiedDate],
        [ProductID]
   ) VALUES (
         Source.[DocumentNode],
        Source.[ModifiedDate],
        Source.[ProductID]
   )
 ;
