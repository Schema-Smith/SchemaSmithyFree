
DECLARE @v_json NVARCHAR(MAX) = '{{Purchasing.PurchaseOrderHeader.tabledata}}';


SET IDENTITY_INSERT [Purchasing].[PurchaseOrderHeader] ON;
MERGE INTO [Purchasing].[PurchaseOrderHeader] AS Target
USING (
  SELECT [EmployeeID],[Freight],[ModifiedDate],[OrderDate],[PurchaseOrderID],[RevisionNumber],[ShipDate],[ShipMethodID],[Status],[SubTotal],[TaxAmt],[VendorID]
    FROM OPENJSON(@v_json)
    WITH (
           [EmployeeID] INT,
           [Freight] MONEY,
           [ModifiedDate] DATETIME,
           [OrderDate] DATETIME,
           [PurchaseOrderID] INT,
           [RevisionNumber] TINYINT,
           [ShipDate] DATETIME,
           [ShipMethodID] INT,
           [Status] TINYINT,
           [SubTotal] MONEY,
           [TaxAmt] MONEY,
           [VendorID] INT
    )
) AS Source
ON Source.[PurchaseOrderID] = Target.[PurchaseOrderID]

WHEN MATCHED AND (NOT (Target.[EmployeeID] = Source.[EmployeeID] OR (Target.[EmployeeID] IS NULL AND Source.[EmployeeID] IS NULL)) OR NOT (Target.[Freight] = Source.[Freight] OR (Target.[Freight] IS NULL AND Source.[Freight] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OrderDate] = Source.[OrderDate] OR (Target.[OrderDate] IS NULL AND Source.[OrderDate] IS NULL)) OR NOT (Target.[RevisionNumber] = Source.[RevisionNumber] OR (Target.[RevisionNumber] IS NULL AND Source.[RevisionNumber] IS NULL)) OR NOT (Target.[ShipDate] = Source.[ShipDate] OR (Target.[ShipDate] IS NULL AND Source.[ShipDate] IS NULL)) OR NOT (Target.[ShipMethodID] = Source.[ShipMethodID] OR (Target.[ShipMethodID] IS NULL AND Source.[ShipMethodID] IS NULL)) OR NOT (Target.[Status] = Source.[Status] OR (Target.[Status] IS NULL AND Source.[Status] IS NULL)) OR NOT (Target.[SubTotal] = Source.[SubTotal] OR (Target.[SubTotal] IS NULL AND Source.[SubTotal] IS NULL)) OR NOT (Target.[TaxAmt] = Source.[TaxAmt] OR (Target.[TaxAmt] IS NULL AND Source.[TaxAmt] IS NULL)) OR NOT (Target.[VendorID] = Source.[VendorID] OR (Target.[VendorID] IS NULL AND Source.[VendorID] IS NULL))) THEN
  UPDATE SET
        [EmployeeID] = Source.[EmployeeID],
        [Freight] = Source.[Freight],
        [ModifiedDate] = Source.[ModifiedDate],
        [OrderDate] = Source.[OrderDate],
        [RevisionNumber] = Source.[RevisionNumber],
        [ShipDate] = Source.[ShipDate],
        [ShipMethodID] = Source.[ShipMethodID],
        [Status] = Source.[Status],
        [SubTotal] = Source.[SubTotal],
        [TaxAmt] = Source.[TaxAmt],
        [VendorID] = Source.[VendorID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [EmployeeID],
        [Freight],
        [ModifiedDate],
        [OrderDate],
        [PurchaseOrderID],
        [RevisionNumber],
        [ShipDate],
        [ShipMethodID],
        [Status],
        [SubTotal],
        [TaxAmt],
        [VendorID]
   ) VALUES (
         Source.[EmployeeID],
        Source.[Freight],
        Source.[ModifiedDate],
        Source.[OrderDate],
        Source.[PurchaseOrderID],
        Source.[RevisionNumber],
        Source.[ShipDate],
        Source.[ShipMethodID],
        Source.[Status],
        Source.[SubTotal],
        Source.[TaxAmt],
        Source.[VendorID]
   )
 ;
SET IDENTITY_INSERT [Purchasing].[PurchaseOrderHeader] OFF;
