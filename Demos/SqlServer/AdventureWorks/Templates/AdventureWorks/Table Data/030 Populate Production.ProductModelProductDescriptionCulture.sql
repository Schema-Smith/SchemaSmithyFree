
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductModelProductDescriptionCulture.tabledata}}';



MERGE INTO [Production].[ProductModelProductDescriptionCulture] AS Target
USING (
  SELECT [CultureID],[ModifiedDate],[ProductDescriptionID],[ProductModelID]
    FROM OPENJSON(@v_json)
    WITH (
           [CultureID] NCHAR(6),
           [ModifiedDate] DATETIME,
           [ProductDescriptionID] INT,
           [ProductModelID] INT
    )
) AS Source
ON Source.[CultureID] = Target.[CultureID] AND Source.[ProductDescriptionID] = Target.[ProductDescriptionID] AND Source.[ProductModelID] = Target.[ProductModelID]

WHEN MATCHED AND (NOT (Target.[CultureID] = Source.[CultureID] OR (Target.[CultureID] IS NULL AND Source.[CultureID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductDescriptionID] = Source.[ProductDescriptionID] OR (Target.[ProductDescriptionID] IS NULL AND Source.[ProductDescriptionID] IS NULL)) OR NOT (Target.[ProductModelID] = Source.[ProductModelID] OR (Target.[ProductModelID] IS NULL AND Source.[ProductModelID] IS NULL))) THEN
  UPDATE SET
        [CultureID] = Source.[CultureID],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductDescriptionID] = Source.[ProductDescriptionID],
        [ProductModelID] = Source.[ProductModelID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CultureID],
        [ModifiedDate],
        [ProductDescriptionID],
        [ProductModelID]
   ) VALUES (
         Source.[CultureID],
        Source.[ModifiedDate],
        Source.[ProductDescriptionID],
        Source.[ProductModelID]
   )
 ;
