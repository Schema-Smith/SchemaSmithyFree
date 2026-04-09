
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.Customer.tabledata}}';


SET IDENTITY_INSERT [Sales].[Customer] ON;
MERGE INTO [Sales].[Customer] AS Target
USING (
  SELECT [CustomerID],[ModifiedDate],[PersonID],[StoreID],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [CustomerID] INT,
           [ModifiedDate] DATETIME,
           [PersonID] INT,
           [rowguid] UNIQUEIDENTIFIER,
           [StoreID] INT,
           [TerritoryID] INT
    )
) AS Source
ON Source.[CustomerID] = Target.[CustomerID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PersonID] = Source.[PersonID] OR (Target.[PersonID] IS NULL AND Source.[PersonID] IS NULL)) OR NOT (Target.[StoreID] = Source.[StoreID] OR (Target.[StoreID] IS NULL AND Source.[StoreID] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [PersonID] = Source.[PersonID],
        [StoreID] = Source.[StoreID],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CustomerID],
        [ModifiedDate],
        [PersonID],
        [StoreID],
        [TerritoryID]
   ) VALUES (
         Source.[CustomerID],
        Source.[ModifiedDate],
        Source.[PersonID],
        Source.[StoreID],
        Source.[TerritoryID]
   )
 ;
SET IDENTITY_INSERT [Sales].[Customer] OFF;
