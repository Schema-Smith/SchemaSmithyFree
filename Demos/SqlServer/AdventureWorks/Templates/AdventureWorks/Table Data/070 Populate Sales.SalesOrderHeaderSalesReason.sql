
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesOrderHeaderSalesReason.tabledata}}';



MERGE INTO [Sales].[SalesOrderHeaderSalesReason] AS Target
USING (
  SELECT [ModifiedDate],[SalesOrderID],[SalesReasonID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [SalesOrderID] INT,
           [SalesReasonID] INT
    )
) AS Source
ON Source.[SalesOrderID] = Target.[SalesOrderID] AND Source.[SalesReasonID] = Target.[SalesReasonID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[SalesOrderID] = Source.[SalesOrderID] OR (Target.[SalesOrderID] IS NULL AND Source.[SalesOrderID] IS NULL)) OR NOT (Target.[SalesReasonID] = Source.[SalesReasonID] OR (Target.[SalesReasonID] IS NULL AND Source.[SalesReasonID] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [SalesOrderID] = Source.[SalesOrderID],
        [SalesReasonID] = Source.[SalesReasonID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [SalesOrderID],
        [SalesReasonID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[SalesOrderID],
        Source.[SalesReasonID]
   )
 ;
