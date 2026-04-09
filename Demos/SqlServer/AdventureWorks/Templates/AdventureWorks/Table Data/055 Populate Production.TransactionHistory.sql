
DECLARE @v_json NVARCHAR(MAX) = '{{Production.TransactionHistory.tabledata}}';


SET IDENTITY_INSERT [Production].[TransactionHistory] ON;
MERGE INTO [Production].[TransactionHistory] AS Target
USING (
  SELECT [ActualCost],[ModifiedDate],[ProductID],[Quantity],[ReferenceOrderID],[ReferenceOrderLineID],[TransactionDate],[TransactionID],[TransactionType]
    FROM OPENJSON(@v_json)
    WITH (
           [ActualCost] MONEY,
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [Quantity] INT,
           [ReferenceOrderID] INT,
           [ReferenceOrderLineID] INT,
           [TransactionDate] DATETIME,
           [TransactionID] INT,
           [TransactionType] NCHAR(1)
    )
) AS Source
ON Source.[TransactionID] = Target.[TransactionID]

WHEN MATCHED AND (NOT (Target.[ActualCost] = Source.[ActualCost] OR (Target.[ActualCost] IS NULL AND Source.[ActualCost] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[Quantity] = Source.[Quantity] OR (Target.[Quantity] IS NULL AND Source.[Quantity] IS NULL)) OR NOT (Target.[ReferenceOrderID] = Source.[ReferenceOrderID] OR (Target.[ReferenceOrderID] IS NULL AND Source.[ReferenceOrderID] IS NULL)) OR NOT (Target.[ReferenceOrderLineID] = Source.[ReferenceOrderLineID] OR (Target.[ReferenceOrderLineID] IS NULL AND Source.[ReferenceOrderLineID] IS NULL)) OR NOT (Target.[TransactionDate] = Source.[TransactionDate] OR (Target.[TransactionDate] IS NULL AND Source.[TransactionDate] IS NULL)) OR NOT (Target.[TransactionType] = Source.[TransactionType] OR (Target.[TransactionType] IS NULL AND Source.[TransactionType] IS NULL))) THEN
  UPDATE SET
        [ActualCost] = Source.[ActualCost],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [Quantity] = Source.[Quantity],
        [ReferenceOrderID] = Source.[ReferenceOrderID],
        [ReferenceOrderLineID] = Source.[ReferenceOrderLineID],
        [TransactionDate] = Source.[TransactionDate],
        [TransactionType] = Source.[TransactionType]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ActualCost],
        [ModifiedDate],
        [ProductID],
        [Quantity],
        [ReferenceOrderID],
        [ReferenceOrderLineID],
        [TransactionDate],
        [TransactionID],
        [TransactionType]
   ) VALUES (
         Source.[ActualCost],
        Source.[ModifiedDate],
        Source.[ProductID],
        Source.[Quantity],
        Source.[ReferenceOrderID],
        Source.[ReferenceOrderLineID],
        Source.[TransactionDate],
        Source.[TransactionID],
        Source.[TransactionType]
   )
 ;
SET IDENTITY_INSERT [Production].[TransactionHistory] OFF;
