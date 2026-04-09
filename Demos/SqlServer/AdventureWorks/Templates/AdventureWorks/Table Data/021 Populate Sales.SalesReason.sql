
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesReason.tabledata}}';


SET IDENTITY_INSERT [Sales].[SalesReason] ON;
MERGE INTO [Sales].[SalesReason] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ReasonType],[SalesReasonID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ReasonType] NAME,
           [SalesReasonID] INT
    )
) AS Source
ON Source.[SalesReasonID] = Target.[SalesReasonID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[ReasonType] = Source.[ReasonType] OR (Target.[ReasonType] IS NULL AND Source.[ReasonType] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ReasonType] = Source.[ReasonType]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [ReasonType],
        [SalesReasonID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[ReasonType],
        Source.[SalesReasonID]
   )
 ;
SET IDENTITY_INSERT [Sales].[SalesReason] OFF;
