
DECLARE @v_json NVARCHAR(MAX) = '';

ALTER TABLE [dbo].[CustomerCustomerDemo] DISABLE TRIGGER ALL;
 
MERGE INTO [dbo].[CustomerCustomerDemo] AS Target
USING (
  SELECT [CustomerID],[CustomerTypeID]
    FROM OPENJSON(@v_json)
    WITH (
           [CustomerID] NCHAR(5),
           [CustomerTypeID] NCHAR(10)
    )
) AS Source
ON Source.[CustomerID] = Target.[CustomerID] AND Source.[CustomerTypeID] = Target.[CustomerTypeID]


WHEN MATCHED AND (NOT (Target.[CustomerID] = Source.[CustomerID] OR (Target.[CustomerID] IS NULL AND Source.[CustomerID] IS NULL)) AND NOT (Target.[CustomerTypeID] = Source.[CustomerTypeID] OR (Target.[CustomerTypeID] IS NULL AND Source.[CustomerTypeID] IS NULL))) THEN
  UPDATE SET
        [CustomerID] = Source.[CustomerID],
        [CustomerTypeID] = Source.[CustomerTypeID]


WHEN NOT MATCHED THEN
  INSERT (
        [CustomerID],
        [CustomerTypeID]
  ) VALUES (
        Source.[CustomerID],
        Source.[CustomerTypeID]  
  )
;
ALTER TABLE [dbo].[CustomerCustomerDemo] ENABLE TRIGGER ALL;
