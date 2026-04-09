
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesPersonQuotaHistory.tabledata}}';



MERGE INTO [Sales].[SalesPersonQuotaHistory] AS Target
USING (
  SELECT [BusinessEntityID],[ModifiedDate],[QuotaDate],[SalesQuota]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [ModifiedDate] DATETIME,
           [QuotaDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesQuota] MONEY
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[QuotaDate] = Target.[QuotaDate]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[QuotaDate] = Source.[QuotaDate] OR (Target.[QuotaDate] IS NULL AND Source.[QuotaDate] IS NULL)) OR NOT (Target.[SalesQuota] = Source.[SalesQuota] OR (Target.[SalesQuota] IS NULL AND Source.[SalesQuota] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ModifiedDate] = Source.[ModifiedDate],
        [QuotaDate] = Source.[QuotaDate],
        [SalesQuota] = Source.[SalesQuota]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [ModifiedDate],
        [QuotaDate],
        [SalesQuota]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[ModifiedDate],
        Source.[QuotaDate],
        Source.[SalesQuota]
   )
 ;
