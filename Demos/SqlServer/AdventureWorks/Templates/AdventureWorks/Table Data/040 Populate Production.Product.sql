
DECLARE @v_json NVARCHAR(MAX) = '{{Production.Product.tabledata}}';


SET IDENTITY_INSERT [Production].[Product] ON;
MERGE INTO [Production].[Product] AS Target
USING (
  SELECT [Class],[Color],[DaysToManufacture],[DiscontinuedDate],[FinishedGoodsFlag],[ListPrice],[MakeFlag],[ModifiedDate],[Name],[ProductID],[ProductLine],[ProductModelID],[ProductNumber],[ProductSubcategoryID],[ReorderPoint],[SafetyStockLevel],[SellEndDate],[SellStartDate],[Size],[SizeUnitMeasureCode],[StandardCost],[Style],[Weight],[WeightUnitMeasureCode]
    FROM OPENJSON(@v_json)
    WITH (
           [Class] NCHAR(2),
           [Color] NVARCHAR(15),
           [DaysToManufacture] INT,
           [DiscontinuedDate] DATETIME,
           [FinishedGoodsFlag] FLAG,
           [ListPrice] MONEY,
           [MakeFlag] FLAG,
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ProductID] INT,
           [ProductLine] NCHAR(2),
           [ProductModelID] INT,
           [ProductNumber] NVARCHAR(25),
           [ProductSubcategoryID] INT,
           [ReorderPoint] SMALLINT,
           [rowguid] UNIQUEIDENTIFIER,
           [SafetyStockLevel] SMALLINT,
           [SellEndDate] DATETIME,
           [SellStartDate] DATETIME,
           [Size] NVARCHAR(5),
           [SizeUnitMeasureCode] NCHAR(3),
           [StandardCost] MONEY,
           [Style] NCHAR(2),
           [Weight] DECIMAL(8, 2),
           [WeightUnitMeasureCode] NCHAR(3)
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID]

WHEN MATCHED AND (NOT (Target.[Class] = Source.[Class] OR (Target.[Class] IS NULL AND Source.[Class] IS NULL)) OR NOT (Target.[Color] = Source.[Color] OR (Target.[Color] IS NULL AND Source.[Color] IS NULL)) OR NOT (Target.[DaysToManufacture] = Source.[DaysToManufacture] OR (Target.[DaysToManufacture] IS NULL AND Source.[DaysToManufacture] IS NULL)) OR NOT (Target.[DiscontinuedDate] = Source.[DiscontinuedDate] OR (Target.[DiscontinuedDate] IS NULL AND Source.[DiscontinuedDate] IS NULL)) OR NOT (Target.[FinishedGoodsFlag] = Source.[FinishedGoodsFlag] OR (Target.[FinishedGoodsFlag] IS NULL AND Source.[FinishedGoodsFlag] IS NULL)) OR NOT (Target.[ListPrice] = Source.[ListPrice] OR (Target.[ListPrice] IS NULL AND Source.[ListPrice] IS NULL)) OR NOT (Target.[MakeFlag] = Source.[MakeFlag] OR (Target.[MakeFlag] IS NULL AND Source.[MakeFlag] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[ProductLine] = Source.[ProductLine] OR (Target.[ProductLine] IS NULL AND Source.[ProductLine] IS NULL)) OR NOT (Target.[ProductModelID] = Source.[ProductModelID] OR (Target.[ProductModelID] IS NULL AND Source.[ProductModelID] IS NULL)) OR NOT (Target.[ProductNumber] = Source.[ProductNumber] OR (Target.[ProductNumber] IS NULL AND Source.[ProductNumber] IS NULL)) OR NOT (Target.[ProductSubcategoryID] = Source.[ProductSubcategoryID] OR (Target.[ProductSubcategoryID] IS NULL AND Source.[ProductSubcategoryID] IS NULL)) OR NOT (Target.[ReorderPoint] = Source.[ReorderPoint] OR (Target.[ReorderPoint] IS NULL AND Source.[ReorderPoint] IS NULL)) OR NOT (Target.[SafetyStockLevel] = Source.[SafetyStockLevel] OR (Target.[SafetyStockLevel] IS NULL AND Source.[SafetyStockLevel] IS NULL)) OR NOT (Target.[SellEndDate] = Source.[SellEndDate] OR (Target.[SellEndDate] IS NULL AND Source.[SellEndDate] IS NULL)) OR NOT (Target.[SellStartDate] = Source.[SellStartDate] OR (Target.[SellStartDate] IS NULL AND Source.[SellStartDate] IS NULL)) OR NOT (Target.[Size] = Source.[Size] OR (Target.[Size] IS NULL AND Source.[Size] IS NULL)) OR NOT (Target.[SizeUnitMeasureCode] = Source.[SizeUnitMeasureCode] OR (Target.[SizeUnitMeasureCode] IS NULL AND Source.[SizeUnitMeasureCode] IS NULL)) OR NOT (Target.[StandardCost] = Source.[StandardCost] OR (Target.[StandardCost] IS NULL AND Source.[StandardCost] IS NULL)) OR NOT (Target.[Style] = Source.[Style] OR (Target.[Style] IS NULL AND Source.[Style] IS NULL)) OR NOT (Target.[Weight] = Source.[Weight] OR (Target.[Weight] IS NULL AND Source.[Weight] IS NULL)) OR NOT (Target.[WeightUnitMeasureCode] = Source.[WeightUnitMeasureCode] OR (Target.[WeightUnitMeasureCode] IS NULL AND Source.[WeightUnitMeasureCode] IS NULL))) THEN
  UPDATE SET
        [Class] = Source.[Class],
        [Color] = Source.[Color],
        [DaysToManufacture] = Source.[DaysToManufacture],
        [DiscontinuedDate] = Source.[DiscontinuedDate],
        [FinishedGoodsFlag] = Source.[FinishedGoodsFlag],
        [ListPrice] = Source.[ListPrice],
        [MakeFlag] = Source.[MakeFlag],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ProductLine] = Source.[ProductLine],
        [ProductModelID] = Source.[ProductModelID],
        [ProductNumber] = Source.[ProductNumber],
        [ProductSubcategoryID] = Source.[ProductSubcategoryID],
        [ReorderPoint] = Source.[ReorderPoint],
        [SafetyStockLevel] = Source.[SafetyStockLevel],
        [SellEndDate] = Source.[SellEndDate],
        [SellStartDate] = Source.[SellStartDate],
        [Size] = Source.[Size],
        [SizeUnitMeasureCode] = Source.[SizeUnitMeasureCode],
        [StandardCost] = Source.[StandardCost],
        [Style] = Source.[Style],
        [Weight] = Source.[Weight],
        [WeightUnitMeasureCode] = Source.[WeightUnitMeasureCode]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Class],
        [Color],
        [DaysToManufacture],
        [DiscontinuedDate],
        [FinishedGoodsFlag],
        [ListPrice],
        [MakeFlag],
        [ModifiedDate],
        [Name],
        [ProductID],
        [ProductLine],
        [ProductModelID],
        [ProductNumber],
        [ProductSubcategoryID],
        [ReorderPoint],
        [SafetyStockLevel],
        [SellEndDate],
        [SellStartDate],
        [Size],
        [SizeUnitMeasureCode],
        [StandardCost],
        [Style],
        [Weight],
        [WeightUnitMeasureCode]
   ) VALUES (
         Source.[Class],
        Source.[Color],
        Source.[DaysToManufacture],
        Source.[DiscontinuedDate],
        Source.[FinishedGoodsFlag],
        Source.[ListPrice],
        Source.[MakeFlag],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ProductID],
        Source.[ProductLine],
        Source.[ProductModelID],
        Source.[ProductNumber],
        Source.[ProductSubcategoryID],
        Source.[ReorderPoint],
        Source.[SafetyStockLevel],
        Source.[SellEndDate],
        Source.[SellStartDate],
        Source.[Size],
        Source.[SizeUnitMeasureCode],
        Source.[StandardCost],
        Source.[Style],
        Source.[Weight],
        Source.[WeightUnitMeasureCode]
   )
 ;
SET IDENTITY_INSERT [Production].[Product] OFF;
