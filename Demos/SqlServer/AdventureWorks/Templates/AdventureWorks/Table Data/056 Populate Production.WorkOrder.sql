
DECLARE @v_json NVARCHAR(MAX) = '{{Production.WorkOrder.tabledata}}';


SET IDENTITY_INSERT [Production].[WorkOrder] ON;
MERGE INTO [Production].[WorkOrder] AS Target
USING (
  SELECT [DueDate],[EndDate],[ModifiedDate],[OrderQty],[ProductID],[ScrappedQty],[ScrapReasonID],[StartDate],[WorkOrderID]
    FROM OPENJSON(@v_json)
    WITH (
           [DueDate] DATETIME,
           [EndDate] DATETIME,
           [ModifiedDate] DATETIME,
           [OrderQty] INT,
           [ProductID] INT,
           [ScrappedQty] SMALLINT,
           [ScrapReasonID] SMALLINT,
           [StartDate] DATETIME,
           [WorkOrderID] INT
    )
) AS Source
ON Source.[WorkOrderID] = Target.[WorkOrderID]

WHEN MATCHED AND (NOT (Target.[DueDate] = Source.[DueDate] OR (Target.[DueDate] IS NULL AND Source.[DueDate] IS NULL)) OR NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OrderQty] = Source.[OrderQty] OR (Target.[OrderQty] IS NULL AND Source.[OrderQty] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[ScrappedQty] = Source.[ScrappedQty] OR (Target.[ScrappedQty] IS NULL AND Source.[ScrappedQty] IS NULL)) OR NOT (Target.[ScrapReasonID] = Source.[ScrapReasonID] OR (Target.[ScrapReasonID] IS NULL AND Source.[ScrapReasonID] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL))) THEN
  UPDATE SET
        [DueDate] = Source.[DueDate],
        [EndDate] = Source.[EndDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [OrderQty] = Source.[OrderQty],
        [ProductID] = Source.[ProductID],
        [ScrappedQty] = Source.[ScrappedQty],
        [ScrapReasonID] = Source.[ScrapReasonID],
        [StartDate] = Source.[StartDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [DueDate],
        [EndDate],
        [ModifiedDate],
        [OrderQty],
        [ProductID],
        [ScrappedQty],
        [ScrapReasonID],
        [StartDate],
        [WorkOrderID]
   ) VALUES (
         Source.[DueDate],
        Source.[EndDate],
        Source.[ModifiedDate],
        Source.[OrderQty],
        Source.[ProductID],
        Source.[ScrappedQty],
        Source.[ScrapReasonID],
        Source.[StartDate],
        Source.[WorkOrderID]
   )
 ;
SET IDENTITY_INSERT [Production].[WorkOrder] OFF;
