
DECLARE @v_json NVARCHAR(MAX) = '{{Purchasing.PurchaseOrderDetail.tabledata}}';


SET IDENTITY_INSERT [Purchasing].[PurchaseOrderDetail] ON;
MERGE INTO [Purchasing].[PurchaseOrderDetail] AS Target
USING (
  SELECT [DueDate],[ModifiedDate],[OrderQty],[ProductID],[PurchaseOrderDetailID],[PurchaseOrderID],[ReceivedQty],[RejectedQty],[UnitPrice]
    FROM OPENJSON(@v_json)
    WITH (
           [DueDate] DATETIME,
           [ModifiedDate] DATETIME,
           [OrderQty] SMALLINT,
           [ProductID] INT,
           [PurchaseOrderDetailID] INT,
           [PurchaseOrderID] INT,
           [ReceivedQty] DECIMAL(8, 2),
           [RejectedQty] DECIMAL(8, 2),
           [UnitPrice] MONEY
    )
) AS Source
ON Source.[PurchaseOrderDetailID] = Target.[PurchaseOrderDetailID] AND Source.[PurchaseOrderID] = Target.[PurchaseOrderID]

WHEN MATCHED AND (NOT (Target.[DueDate] = Source.[DueDate] OR (Target.[DueDate] IS NULL AND Source.[DueDate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OrderQty] = Source.[OrderQty] OR (Target.[OrderQty] IS NULL AND Source.[OrderQty] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[PurchaseOrderID] = Source.[PurchaseOrderID] OR (Target.[PurchaseOrderID] IS NULL AND Source.[PurchaseOrderID] IS NULL)) OR NOT (Target.[ReceivedQty] = Source.[ReceivedQty] OR (Target.[ReceivedQty] IS NULL AND Source.[ReceivedQty] IS NULL)) OR NOT (Target.[RejectedQty] = Source.[RejectedQty] OR (Target.[RejectedQty] IS NULL AND Source.[RejectedQty] IS NULL)) OR NOT (Target.[UnitPrice] = Source.[UnitPrice] OR (Target.[UnitPrice] IS NULL AND Source.[UnitPrice] IS NULL))) THEN
  UPDATE SET
        [DueDate] = Source.[DueDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [OrderQty] = Source.[OrderQty],
        [ProductID] = Source.[ProductID],
        [PurchaseOrderID] = Source.[PurchaseOrderID],
        [ReceivedQty] = Source.[ReceivedQty],
        [RejectedQty] = Source.[RejectedQty],
        [UnitPrice] = Source.[UnitPrice]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [DueDate],
        [ModifiedDate],
        [OrderQty],
        [ProductID],
        [PurchaseOrderDetailID],
        [PurchaseOrderID],
        [ReceivedQty],
        [RejectedQty],
        [UnitPrice]
   ) VALUES (
         Source.[DueDate],
        Source.[ModifiedDate],
        Source.[OrderQty],
        Source.[ProductID],
        Source.[PurchaseOrderDetailID],
        Source.[PurchaseOrderID],
        Source.[ReceivedQty],
        Source.[RejectedQty],
        Source.[UnitPrice]
   )
 ;
SET IDENTITY_INSERT [Purchasing].[PurchaseOrderDetail] OFF;
