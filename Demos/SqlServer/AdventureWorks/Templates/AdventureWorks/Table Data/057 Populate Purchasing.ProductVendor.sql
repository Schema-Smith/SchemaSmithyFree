
DECLARE @v_json NVARCHAR(MAX) = '{{Purchasing.ProductVendor.tabledata}}';



MERGE INTO [Purchasing].[ProductVendor] AS Target
USING (
  SELECT [AverageLeadTime],[BusinessEntityID],[LastReceiptCost],[LastReceiptDate],[MaxOrderQty],[MinOrderQty],[ModifiedDate],[OnOrderQty],[ProductID],[StandardPrice],[UnitMeasureCode]
    FROM OPENJSON(@v_json)
    WITH (
           [AverageLeadTime] INT,
           [BusinessEntityID] INT,
           [LastReceiptCost] MONEY,
           [LastReceiptDate] DATETIME,
           [MaxOrderQty] INT,
           [MinOrderQty] INT,
           [ModifiedDate] DATETIME,
           [OnOrderQty] INT,
           [ProductID] INT,
           [StandardPrice] MONEY,
           [UnitMeasureCode] NCHAR(3)
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[ProductID] = Target.[ProductID]

WHEN MATCHED AND (NOT (Target.[AverageLeadTime] = Source.[AverageLeadTime] OR (Target.[AverageLeadTime] IS NULL AND Source.[AverageLeadTime] IS NULL)) OR NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[LastReceiptCost] = Source.[LastReceiptCost] OR (Target.[LastReceiptCost] IS NULL AND Source.[LastReceiptCost] IS NULL)) OR NOT (Target.[LastReceiptDate] = Source.[LastReceiptDate] OR (Target.[LastReceiptDate] IS NULL AND Source.[LastReceiptDate] IS NULL)) OR NOT (Target.[MaxOrderQty] = Source.[MaxOrderQty] OR (Target.[MaxOrderQty] IS NULL AND Source.[MaxOrderQty] IS NULL)) OR NOT (Target.[MinOrderQty] = Source.[MinOrderQty] OR (Target.[MinOrderQty] IS NULL AND Source.[MinOrderQty] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OnOrderQty] = Source.[OnOrderQty] OR (Target.[OnOrderQty] IS NULL AND Source.[OnOrderQty] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[StandardPrice] = Source.[StandardPrice] OR (Target.[StandardPrice] IS NULL AND Source.[StandardPrice] IS NULL)) OR NOT (Target.[UnitMeasureCode] = Source.[UnitMeasureCode] OR (Target.[UnitMeasureCode] IS NULL AND Source.[UnitMeasureCode] IS NULL))) THEN
  UPDATE SET
        [AverageLeadTime] = Source.[AverageLeadTime],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [LastReceiptCost] = Source.[LastReceiptCost],
        [LastReceiptDate] = Source.[LastReceiptDate],
        [MaxOrderQty] = Source.[MaxOrderQty],
        [MinOrderQty] = Source.[MinOrderQty],
        [ModifiedDate] = Source.[ModifiedDate],
        [OnOrderQty] = Source.[OnOrderQty],
        [ProductID] = Source.[ProductID],
        [StandardPrice] = Source.[StandardPrice],
        [UnitMeasureCode] = Source.[UnitMeasureCode]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AverageLeadTime],
        [BusinessEntityID],
        [LastReceiptCost],
        [LastReceiptDate],
        [MaxOrderQty],
        [MinOrderQty],
        [ModifiedDate],
        [OnOrderQty],
        [ProductID],
        [StandardPrice],
        [UnitMeasureCode]
   ) VALUES (
         Source.[AverageLeadTime],
        Source.[BusinessEntityID],
        Source.[LastReceiptCost],
        Source.[LastReceiptDate],
        Source.[MaxOrderQty],
        Source.[MinOrderQty],
        Source.[ModifiedDate],
        Source.[OnOrderQty],
        Source.[ProductID],
        Source.[StandardPrice],
        Source.[UnitMeasureCode]
   )
 ;
