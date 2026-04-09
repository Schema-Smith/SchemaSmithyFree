
DECLARE @v_json NVARCHAR(MAX) = '{{Production.WorkOrderRouting.tabledata}}';



MERGE INTO [Production].[WorkOrderRouting] AS Target
USING (
  SELECT [ActualCost],[ActualEndDate],[ActualResourceHrs],[ActualStartDate],[LocationID],[ModifiedDate],[OperationSequence],[PlannedCost],[ProductID],[ScheduledEndDate],[ScheduledStartDate],[WorkOrderID]
    FROM OPENJSON(@v_json)
    WITH (
           [ActualCost] MONEY,
           [ActualEndDate] DATETIME,
           [ActualResourceHrs] DECIMAL(9, 4),
           [ActualStartDate] DATETIME,
           [LocationID] SMALLINT,
           [ModifiedDate] DATETIME,
           [OperationSequence] SMALLINT,
           [PlannedCost] MONEY,
           [ProductID] INT,
           [ScheduledEndDate] DATETIME,
           [ScheduledStartDate] DATETIME,
           [WorkOrderID] INT
    )
) AS Source
ON Source.[OperationSequence] = Target.[OperationSequence] AND Source.[ProductID] = Target.[ProductID] AND Source.[WorkOrderID] = Target.[WorkOrderID]

WHEN MATCHED AND (NOT (Target.[ActualCost] = Source.[ActualCost] OR (Target.[ActualCost] IS NULL AND Source.[ActualCost] IS NULL)) OR NOT (Target.[ActualEndDate] = Source.[ActualEndDate] OR (Target.[ActualEndDate] IS NULL AND Source.[ActualEndDate] IS NULL)) OR NOT (Target.[ActualResourceHrs] = Source.[ActualResourceHrs] OR (Target.[ActualResourceHrs] IS NULL AND Source.[ActualResourceHrs] IS NULL)) OR NOT (Target.[ActualStartDate] = Source.[ActualStartDate] OR (Target.[ActualStartDate] IS NULL AND Source.[ActualStartDate] IS NULL)) OR NOT (Target.[LocationID] = Source.[LocationID] OR (Target.[LocationID] IS NULL AND Source.[LocationID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[OperationSequence] = Source.[OperationSequence] OR (Target.[OperationSequence] IS NULL AND Source.[OperationSequence] IS NULL)) OR NOT (Target.[PlannedCost] = Source.[PlannedCost] OR (Target.[PlannedCost] IS NULL AND Source.[PlannedCost] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[ScheduledEndDate] = Source.[ScheduledEndDate] OR (Target.[ScheduledEndDate] IS NULL AND Source.[ScheduledEndDate] IS NULL)) OR NOT (Target.[ScheduledStartDate] = Source.[ScheduledStartDate] OR (Target.[ScheduledStartDate] IS NULL AND Source.[ScheduledStartDate] IS NULL)) OR NOT (Target.[WorkOrderID] = Source.[WorkOrderID] OR (Target.[WorkOrderID] IS NULL AND Source.[WorkOrderID] IS NULL))) THEN
  UPDATE SET
        [ActualCost] = Source.[ActualCost],
        [ActualEndDate] = Source.[ActualEndDate],
        [ActualResourceHrs] = Source.[ActualResourceHrs],
        [ActualStartDate] = Source.[ActualStartDate],
        [LocationID] = Source.[LocationID],
        [ModifiedDate] = Source.[ModifiedDate],
        [OperationSequence] = Source.[OperationSequence],
        [PlannedCost] = Source.[PlannedCost],
        [ProductID] = Source.[ProductID],
        [ScheduledEndDate] = Source.[ScheduledEndDate],
        [ScheduledStartDate] = Source.[ScheduledStartDate],
        [WorkOrderID] = Source.[WorkOrderID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ActualCost],
        [ActualEndDate],
        [ActualResourceHrs],
        [ActualStartDate],
        [LocationID],
        [ModifiedDate],
        [OperationSequence],
        [PlannedCost],
        [ProductID],
        [ScheduledEndDate],
        [ScheduledStartDate],
        [WorkOrderID]
   ) VALUES (
         Source.[ActualCost],
        Source.[ActualEndDate],
        Source.[ActualResourceHrs],
        Source.[ActualStartDate],
        Source.[LocationID],
        Source.[ModifiedDate],
        Source.[OperationSequence],
        Source.[PlannedCost],
        Source.[ProductID],
        Source.[ScheduledEndDate],
        Source.[ScheduledStartDate],
        Source.[WorkOrderID]
   )
 ;
