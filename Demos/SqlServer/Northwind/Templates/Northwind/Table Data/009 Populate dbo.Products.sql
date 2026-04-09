
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Products.tabledata}}';


SET IDENTITY_INSERT [dbo].[Products] ON;
MERGE INTO [dbo].[Products] AS Target
USING (
  SELECT [CategoryID],[Discontinued],[ProductID],[ProductName],[QuantityPerUnit],[ReorderLevel],[SupplierID],[UnitPrice],[UnitsInStock],[UnitsOnOrder]
    FROM OPENJSON(@v_json)
    WITH (
           [CategoryID] INT,
           [Discontinued] BIT,
           [ProductID] INT,
           [ProductName] NVARCHAR(40),
           [QuantityPerUnit] NVARCHAR(20),
           [ReorderLevel] SMALLINT,
           [SupplierID] INT,
           [UnitPrice] MONEY,
           [UnitsInStock] SMALLINT,
           [UnitsOnOrder] SMALLINT
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID]

WHEN MATCHED AND (NOT (Target.[CategoryID] = Source.[CategoryID] OR (Target.[CategoryID] IS NULL AND Source.[CategoryID] IS NULL)) OR NOT (Target.[Discontinued] = Source.[Discontinued] OR (Target.[Discontinued] IS NULL AND Source.[Discontinued] IS NULL)) OR NOT (Target.[ProductName] = Source.[ProductName] OR (Target.[ProductName] IS NULL AND Source.[ProductName] IS NULL)) OR NOT (Target.[QuantityPerUnit] = Source.[QuantityPerUnit] OR (Target.[QuantityPerUnit] IS NULL AND Source.[QuantityPerUnit] IS NULL)) OR NOT (Target.[ReorderLevel] = Source.[ReorderLevel] OR (Target.[ReorderLevel] IS NULL AND Source.[ReorderLevel] IS NULL)) OR NOT (Target.[SupplierID] = Source.[SupplierID] OR (Target.[SupplierID] IS NULL AND Source.[SupplierID] IS NULL)) OR NOT (Target.[UnitPrice] = Source.[UnitPrice] OR (Target.[UnitPrice] IS NULL AND Source.[UnitPrice] IS NULL)) OR NOT (Target.[UnitsInStock] = Source.[UnitsInStock] OR (Target.[UnitsInStock] IS NULL AND Source.[UnitsInStock] IS NULL)) OR NOT (Target.[UnitsOnOrder] = Source.[UnitsOnOrder] OR (Target.[UnitsOnOrder] IS NULL AND Source.[UnitsOnOrder] IS NULL))) THEN
  UPDATE SET
        [CategoryID] = Source.[CategoryID],
        [Discontinued] = Source.[Discontinued],
        [ProductName] = Source.[ProductName],
        [QuantityPerUnit] = Source.[QuantityPerUnit],
        [ReorderLevel] = Source.[ReorderLevel],
        [SupplierID] = Source.[SupplierID],
        [UnitPrice] = Source.[UnitPrice],
        [UnitsInStock] = Source.[UnitsInStock],
        [UnitsOnOrder] = Source.[UnitsOnOrder]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CategoryID],
        [Discontinued],
        [ProductID],
        [ProductName],
        [QuantityPerUnit],
        [ReorderLevel],
        [SupplierID],
        [UnitPrice],
        [UnitsInStock],
        [UnitsOnOrder]
   ) VALUES (
         Source.[CategoryID],
        Source.[Discontinued],
        Source.[ProductID],
        Source.[ProductName],
        Source.[QuantityPerUnit],
        Source.[ReorderLevel],
        Source.[SupplierID],
        Source.[UnitPrice],
        Source.[UnitsInStock],
        Source.[UnitsOnOrder]
   )
 ;
SET IDENTITY_INSERT [dbo].[Products] OFF;
