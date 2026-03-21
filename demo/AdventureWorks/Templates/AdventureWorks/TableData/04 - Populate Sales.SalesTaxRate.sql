DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST + Alberta Provincial Tax","SalesTaxRateID":1,"StateProvinceID":1,"TaxRate":14.0000,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST + Ontario Provincial Tax","SalesTaxRateID":2,"StateProvinceID":57,"TaxRate":14.2500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST + Quebec Provincial Tax","SalesTaxRateID":3,"StateProvinceID":63,"TaxRate":14.2500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":4,"StateProvinceID":1,"TaxRate":7.0000,"TaxType":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":5,"StateProvinceID":57,"TaxRate":7.0000,"TaxType":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":6,"StateProvinceID":63,"TaxRate":7.0000,"TaxType":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":7,"StateProvinceID":7,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":8,"StateProvinceID":29,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":9,"StateProvinceID":31,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":10,"StateProvinceID":41,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":11,"StateProvinceID":45,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":12,"StateProvinceID":49,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":13,"StateProvinceID":51,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":16,"StateProvinceID":69,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian GST","SalesTaxRateID":17,"StateProvinceID":83,"TaxRate":7.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Arizona State Sales Tax","SalesTaxRateID":18,"StateProvinceID":6,"TaxRate":7.7500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"California State Sales Tax","SalesTaxRateID":19,"StateProvinceID":9,"TaxRate":8.7500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Florida State Sales Tax","SalesTaxRateID":20,"StateProvinceID":15,"TaxRate":8.0000,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Massachusetts State Sales Tax","SalesTaxRateID":21,"StateProvinceID":30,"TaxRate":7.0000,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Michigan State Sales Tax","SalesTaxRateID":22,"StateProvinceID":35,"TaxRate":7.2500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Minnesota State Sales Tax","SalesTaxRateID":23,"StateProvinceID":36,"TaxRate":6.7500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Tennessese State Sales Tax","SalesTaxRateID":24,"StateProvinceID":72,"TaxRate":7.2500,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Texas State Sales Tax","SalesTaxRateID":25,"StateProvinceID":73,"TaxRate":7.5000,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Utah State Sales Tax","SalesTaxRateID":26,"StateProvinceID":74,"TaxRate":5.0000,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Washington State Sales Tax","SalesTaxRateID":27,"StateProvinceID":79,"TaxRate":8.8000,"TaxType":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Taxable Supply","SalesTaxRateID":28,"StateProvinceID":77,"TaxRate":10.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Germany Output Tax","SalesTaxRateID":29,"StateProvinceID":20,"TaxRate":16.0000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"France Output Tax","SalesTaxRateID":30,"StateProvinceID":84,"TaxRate":19.6000,"TaxType":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"United Kingdom Output Tax","SalesTaxRateID":31,"StateProvinceID":14,"TaxRate":17.5000,"TaxType":3}
]';

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
WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[StateProvinceID] = Source.[StateProvinceID] OR (Target.[StateProvinceID] IS NULL AND Source.[StateProvinceID] IS NULL)) AND NOT (Target.[TaxRate] = Source.[TaxRate] OR (Target.[TaxRate] IS NULL AND Source.[TaxRate] IS NULL)) AND NOT (Target.[TaxType] = Source.[TaxType] OR (Target.[TaxType] IS NULL AND Source.[TaxType] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [StateProvinceID] = Source.[StateProvinceID],
        [TaxRate] = Source.[TaxRate],
        [TaxType] = Source.[TaxType]
WHEN NOT MATCHED THEN
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
  );
SET IDENTITY_INSERT [Sales].[SalesTaxRate] OFF; 
