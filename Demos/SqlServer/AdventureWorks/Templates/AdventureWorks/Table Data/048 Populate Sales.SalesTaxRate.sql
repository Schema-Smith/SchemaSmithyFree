
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SalesTaxRate.tabledata}}';


SET IDENTITY_INSERT [Sales].[SalesTaxRate] ON;
MERGE INTO [Sales].[SalesTaxRate] AS Target
USING (
  SELECT [ModifiedDate],[Name],[SalesTaxRateID],[StateProvinceID],[TaxRate],[TaxType]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesTaxRateID] INT,
           [StateProvinceID] INT,
           [TaxRate] SMALLMONEY,
           [TaxType] TINYINT
    )
) AS Source
ON Source.[SalesTaxRateID] = Target.[SalesTaxRateID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[StateProvinceID] = Source.[StateProvinceID] OR (Target.[StateProvinceID] IS NULL AND Source.[StateProvinceID] IS NULL)) OR NOT (Target.[TaxRate] = Source.[TaxRate] OR (Target.[TaxRate] IS NULL AND Source.[TaxRate] IS NULL)) OR NOT (Target.[TaxType] = Source.[TaxType] OR (Target.[TaxType] IS NULL AND Source.[TaxType] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [StateProvinceID] = Source.[StateProvinceID],
        [TaxRate] = Source.[TaxRate],
        [TaxType] = Source.[TaxType]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [SalesTaxRateID],
        [StateProvinceID],
        [TaxRate],
        [TaxType]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[SalesTaxRateID],
        Source.[StateProvinceID],
        Source.[TaxRate],
        Source.[TaxType]
   )
 ;
SET IDENTITY_INSERT [Sales].[SalesTaxRate] OFF;
