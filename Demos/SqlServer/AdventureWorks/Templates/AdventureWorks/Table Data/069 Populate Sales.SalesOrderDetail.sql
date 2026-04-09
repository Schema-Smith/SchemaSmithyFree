
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesOrderDetail.tabledata}}';


SET IDENTITY_INSERT [Sales].[SalesOrderDetail] ON;
MERGE INTO [Sales].[SalesOrderDetail] AS Target
USING (
  SELECT [CarrierTrackingNumber],[ModifiedDate],[OrderQty],[ProductID],[SalesOrderDetailID],[SalesOrderID],[SpecialOfferID],[UnitPrice],[UnitPriceDiscount]
    FROM OPENJSON(@v_json)
    WITH (
           [CarrierTrackingNumber] NVARCHAR(25),
           [ModifiedDate] DATETIME,
           [OrderQty] SMALLINT,
           [ProductID] INT,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesOrderDetailID] INT,
           [SalesOrderID] INT,
           [SpecialOfferID] INT,
           [UnitPrice] MONEY,
           [UnitPriceDiscount] MONEY
    )
) AS Source
ON Source.[SalesOrderDetailID] = Target.[SalesOrderDetailID] AND Source.[SalesOrderID] = Target.[SalesOrderID]

WHEN MATCHED AND (NOT (Target.[CarrierTrackingNumber] = Source.[CarrierTrackingNumber] OR (Target.[CarrierTrackingNumber] IS NULL AND Source.[CarrierTrackingNumber] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OrderQty] = Source.[OrderQty] OR (Target.[OrderQty] IS NULL AND Source.[OrderQty] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[SalesOrderID] = Source.[SalesOrderID] OR (Target.[SalesOrderID] IS NULL AND Source.[SalesOrderID] IS NULL)) OR NOT (Target.[SpecialOfferID] = Source.[SpecialOfferID] OR (Target.[SpecialOfferID] IS NULL AND Source.[SpecialOfferID] IS NULL)) OR NOT (Target.[UnitPrice] = Source.[UnitPrice] OR (Target.[UnitPrice] IS NULL AND Source.[UnitPrice] IS NULL)) OR NOT (Target.[UnitPriceDiscount] = Source.[UnitPriceDiscount] OR (Target.[UnitPriceDiscount] IS NULL AND Source.[UnitPriceDiscount] IS NULL))) THEN
  UPDATE SET
        [CarrierTrackingNumber] = Source.[CarrierTrackingNumber],
        [ModifiedDate] = Source.[ModifiedDate],
        [OrderQty] = Source.[OrderQty],
        [ProductID] = Source.[ProductID],
        [SalesOrderID] = Source.[SalesOrderID],
        [SpecialOfferID] = Source.[SpecialOfferID],
        [UnitPrice] = Source.[UnitPrice],
        [UnitPriceDiscount] = Source.[UnitPriceDiscount]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CarrierTrackingNumber],
        [ModifiedDate],
        [OrderQty],
        [ProductID],
        [SalesOrderDetailID],
        [SalesOrderID],
        [SpecialOfferID],
        [UnitPrice],
        [UnitPriceDiscount]
   ) VALUES (
         Source.[CarrierTrackingNumber],
        Source.[ModifiedDate],
        Source.[OrderQty],
        Source.[ProductID],
        Source.[SalesOrderDetailID],
        Source.[SalesOrderID],
        Source.[SpecialOfferID],
        Source.[UnitPrice],
        Source.[UnitPriceDiscount]
   )
 ;
SET IDENTITY_INSERT [Sales].[SalesOrderDetail] OFF;
