
DECLARE @v_json NVARCHAR(MAX) = '{{Purchasing.ShipMethod.tabledata}}';


SET IDENTITY_INSERT [Purchasing].[ShipMethod] ON;
MERGE INTO [Purchasing].[ShipMethod] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ShipBase],[ShipMethodID],[ShipRate]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER,
           [ShipBase] MONEY,
           [ShipMethodID] INT,
           [ShipRate] MONEY
    )
) AS Source
ON Source.[ShipMethodID] = Target.[ShipMethodID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[ShipBase] = Source.[ShipBase] OR (Target.[ShipBase] IS NULL AND Source.[ShipBase] IS NULL)) OR NOT (Target.[ShipRate] = Source.[ShipRate] OR (Target.[ShipRate] IS NULL AND Source.[ShipRate] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ShipBase] = Source.[ShipBase],
        [ShipRate] = Source.[ShipRate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [ShipBase],
        [ShipMethodID],
        [ShipRate]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[ShipBase],
        Source.[ShipMethodID],
        Source.[ShipRate]
   )
 ;
SET IDENTITY_INSERT [Purchasing].[ShipMethod] OFF;
