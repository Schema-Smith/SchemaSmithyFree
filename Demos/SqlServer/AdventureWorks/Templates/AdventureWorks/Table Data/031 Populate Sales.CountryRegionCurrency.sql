
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.CountryRegionCurrency.tabledata}}';



MERGE INTO [Sales].[CountryRegionCurrency] AS Target
USING (
  SELECT [CountryRegionCode],[CurrencyCode],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [CountryRegionCode] NVARCHAR(3),
           [CurrencyCode] NCHAR(3),
           [ModifiedDate] DATETIME
    )
) AS Source
ON Source.[CountryRegionCode] = Target.[CountryRegionCode] AND Source.[CurrencyCode] = Target.[CurrencyCode]

WHEN MATCHED AND (NOT (Target.[CountryRegionCode] = Source.[CountryRegionCode] OR (Target.[CountryRegionCode] IS NULL AND Source.[CountryRegionCode] IS NULL)) OR NOT (Target.[CurrencyCode] = Source.[CurrencyCode] OR (Target.[CurrencyCode] IS NULL AND Source.[CurrencyCode] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [CountryRegionCode] = Source.[CountryRegionCode],
        [CurrencyCode] = Source.[CurrencyCode],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CountryRegionCode],
        [CurrencyCode],
        [ModifiedDate]
   ) VALUES (
         Source.[CountryRegionCode],
        Source.[CurrencyCode],
        Source.[ModifiedDate]
   )
 ;
