
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SpecialOfferProduct.tabledata}}';



MERGE INTO [Sales].[SpecialOfferProduct] AS Target
USING (
  SELECT [ModifiedDate],[ProductID],[SpecialOfferID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [rowguid] UNIQUEIDENTIFIER,
           [SpecialOfferID] INT
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID] AND Source.[SpecialOfferID] = Target.[SpecialOfferID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[SpecialOfferID] = Source.[SpecialOfferID] OR (Target.[SpecialOfferID] IS NULL AND Source.[SpecialOfferID] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [SpecialOfferID] = Source.[SpecialOfferID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [ProductID],
        [SpecialOfferID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[ProductID],
        Source.[SpecialOfferID]
   )
 ;
