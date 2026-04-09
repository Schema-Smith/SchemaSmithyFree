
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Order Details.tabledata}}';



MERGE INTO [dbo].[Order Details] AS Target
USING (
  SELECT [Discount],[OrderID],[ProductID],[Quantity],[UnitPrice]
    FROM OPENJSON(@v_json)
    WITH (
           [Discount] REAL,
           [OrderID] INT,
           [ProductID] INT,
           [Quantity] SMALLINT,
           [UnitPrice] MONEY
    )
) AS Source
ON Source.[OrderID] = Target.[OrderID] AND Source.[ProductID] = Target.[ProductID]

WHEN MATCHED AND (NOT (Target.[Discount] = Source.[Discount] OR (Target.[Discount] IS NULL AND Source.[Discount] IS NULL)) OR NOT (Target.[OrderID] = Source.[OrderID] OR (Target.[OrderID] IS NULL AND Source.[OrderID] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[Quantity] = Source.[Quantity] OR (Target.[Quantity] IS NULL AND Source.[Quantity] IS NULL)) OR NOT (Target.[UnitPrice] = Source.[UnitPrice] OR (Target.[UnitPrice] IS NULL AND Source.[UnitPrice] IS NULL))) THEN
  UPDATE SET
        [Discount] = Source.[Discount],
        [OrderID] = Source.[OrderID],
        [ProductID] = Source.[ProductID],
        [Quantity] = Source.[Quantity],
        [UnitPrice] = Source.[UnitPrice]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Discount],
        [OrderID],
        [ProductID],
        [Quantity],
        [UnitPrice]
   ) VALUES (
         Source.[Discount],
        Source.[OrderID],
        Source.[ProductID],
        Source.[Quantity],
        Source.[UnitPrice]
   )
 ;
