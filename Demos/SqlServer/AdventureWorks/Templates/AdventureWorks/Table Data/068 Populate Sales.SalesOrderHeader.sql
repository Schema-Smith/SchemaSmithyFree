
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesOrderHeader.tabledata}}';


SET IDENTITY_INSERT [Sales].[SalesOrderHeader] ON;
MERGE INTO [Sales].[SalesOrderHeader] AS Target
USING (
  SELECT [AccountNumber],[BillToAddressID],[Comment],[CreditCardApprovalCode],[CreditCardID],[CurrencyRateID],[CustomerID],[DueDate],[Freight],[ModifiedDate],[OnlineOrderFlag],[OrderDate],[PurchaseOrderNumber],[RevisionNumber],[SalesOrderID],[SalesPersonID],[ShipDate],[ShipMethodID],[ShipToAddressID],[Status],[SubTotal],[TaxAmt],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [AccountNumber] ACCOUNTNUMBER,
           [BillToAddressID] INT,
           [Comment] NVARCHAR(128),
           [CreditCardApprovalCode] VARCHAR(15),
           [CreditCardID] INT,
           [CurrencyRateID] INT,
           [CustomerID] INT,
           [DueDate] DATETIME,
           [Freight] MONEY,
           [ModifiedDate] DATETIME,
           [OnlineOrderFlag] FLAG,
           [OrderDate] DATETIME,
           [PurchaseOrderNumber] ORDERNUMBER,
           [RevisionNumber] TINYINT,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesOrderID] INT,
           [SalesPersonID] INT,
           [ShipDate] DATETIME,
           [ShipMethodID] INT,
           [ShipToAddressID] INT,
           [Status] TINYINT,
           [SubTotal] MONEY,
           [TaxAmt] MONEY,
           [TerritoryID] INT
    )
) AS Source
ON Source.[SalesOrderID] = Target.[SalesOrderID]

WHEN MATCHED AND (NOT (Target.[AccountNumber] = Source.[AccountNumber] OR (Target.[AccountNumber] IS NULL AND Source.[AccountNumber] IS NULL)) OR NOT (Target.[BillToAddressID] = Source.[BillToAddressID] OR (Target.[BillToAddressID] IS NULL AND Source.[BillToAddressID] IS NULL)) OR NOT (Target.[Comment] = Source.[Comment] OR (Target.[Comment] IS NULL AND Source.[Comment] IS NULL)) OR NOT (Target.[CreditCardApprovalCode] = Source.[CreditCardApprovalCode] OR (Target.[CreditCardApprovalCode] IS NULL AND Source.[CreditCardApprovalCode] IS NULL)) OR NOT (Target.[CreditCardID] = Source.[CreditCardID] OR (Target.[CreditCardID] IS NULL AND Source.[CreditCardID] IS NULL)) OR NOT (Target.[CurrencyRateID] = Source.[CurrencyRateID] OR (Target.[CurrencyRateID] IS NULL AND Source.[CurrencyRateID] IS NULL)) OR NOT (Target.[CustomerID] = Source.[CustomerID] OR (Target.[CustomerID] IS NULL AND Source.[CustomerID] IS NULL)) OR NOT (Target.[DueDate] = Source.[DueDate] OR (Target.[DueDate] IS NULL AND Source.[DueDate] IS NULL)) OR NOT (Target.[Freight] = Source.[Freight] OR (Target.[Freight] IS NULL AND Source.[Freight] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OnlineOrderFlag] = Source.[OnlineOrderFlag] OR (Target.[OnlineOrderFlag] IS NULL AND Source.[OnlineOrderFlag] IS NULL)) OR NOT (Target.[OrderDate] = Source.[OrderDate] OR (Target.[OrderDate] IS NULL AND Source.[OrderDate] IS NULL)) OR NOT (Target.[PurchaseOrderNumber] = Source.[PurchaseOrderNumber] OR (Target.[PurchaseOrderNumber] IS NULL AND Source.[PurchaseOrderNumber] IS NULL)) OR NOT (Target.[RevisionNumber] = Source.[RevisionNumber] OR (Target.[RevisionNumber] IS NULL AND Source.[RevisionNumber] IS NULL)) OR NOT (Target.[SalesPersonID] = Source.[SalesPersonID] OR (Target.[SalesPersonID] IS NULL AND Source.[SalesPersonID] IS NULL)) OR NOT (Target.[ShipDate] = Source.[ShipDate] OR (Target.[ShipDate] IS NULL AND Source.[ShipDate] IS NULL)) OR NOT (Target.[ShipMethodID] = Source.[ShipMethodID] OR (Target.[ShipMethodID] IS NULL AND Source.[ShipMethodID] IS NULL)) OR NOT (Target.[ShipToAddressID] = Source.[ShipToAddressID] OR (Target.[ShipToAddressID] IS NULL AND Source.[ShipToAddressID] IS NULL)) OR NOT (Target.[Status] = Source.[Status] OR (Target.[Status] IS NULL AND Source.[Status] IS NULL)) OR NOT (Target.[SubTotal] = Source.[SubTotal] OR (Target.[SubTotal] IS NULL AND Source.[SubTotal] IS NULL)) OR NOT (Target.[TaxAmt] = Source.[TaxAmt] OR (Target.[TaxAmt] IS NULL AND Source.[TaxAmt] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [AccountNumber] = Source.[AccountNumber],
        [BillToAddressID] = Source.[BillToAddressID],
        [Comment] = Source.[Comment],
        [CreditCardApprovalCode] = Source.[CreditCardApprovalCode],
        [CreditCardID] = Source.[CreditCardID],
        [CurrencyRateID] = Source.[CurrencyRateID],
        [CustomerID] = Source.[CustomerID],
        [DueDate] = Source.[DueDate],
        [Freight] = Source.[Freight],
        [ModifiedDate] = Source.[ModifiedDate],
        [OnlineOrderFlag] = Source.[OnlineOrderFlag],
        [OrderDate] = Source.[OrderDate],
        [PurchaseOrderNumber] = Source.[PurchaseOrderNumber],
        [RevisionNumber] = Source.[RevisionNumber],
        [SalesPersonID] = Source.[SalesPersonID],
        [ShipDate] = Source.[ShipDate],
        [ShipMethodID] = Source.[ShipMethodID],
        [ShipToAddressID] = Source.[ShipToAddressID],
        [Status] = Source.[Status],
        [SubTotal] = Source.[SubTotal],
        [TaxAmt] = Source.[TaxAmt],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AccountNumber],
        [BillToAddressID],
        [Comment],
        [CreditCardApprovalCode],
        [CreditCardID],
        [CurrencyRateID],
        [CustomerID],
        [DueDate],
        [Freight],
        [ModifiedDate],
        [OnlineOrderFlag],
        [OrderDate],
        [PurchaseOrderNumber],
        [RevisionNumber],
        [SalesOrderID],
        [SalesPersonID],
        [ShipDate],
        [ShipMethodID],
        [ShipToAddressID],
        [Status],
        [SubTotal],
        [TaxAmt],
        [TerritoryID]
   ) VALUES (
         Source.[AccountNumber],
        Source.[BillToAddressID],
        Source.[Comment],
        Source.[CreditCardApprovalCode],
        Source.[CreditCardID],
        Source.[CurrencyRateID],
        Source.[CustomerID],
        Source.[DueDate],
        Source.[Freight],
        Source.[ModifiedDate],
        Source.[OnlineOrderFlag],
        Source.[OrderDate],
        Source.[PurchaseOrderNumber],
        Source.[RevisionNumber],
        Source.[SalesOrderID],
        Source.[SalesPersonID],
        Source.[ShipDate],
        Source.[ShipMethodID],
        Source.[ShipToAddressID],
        Source.[Status],
        Source.[SubTotal],
        Source.[TaxAmt],
        Source.[TerritoryID]
   )
 ;
SET IDENTITY_INSERT [Sales].[SalesOrderHeader] OFF;
