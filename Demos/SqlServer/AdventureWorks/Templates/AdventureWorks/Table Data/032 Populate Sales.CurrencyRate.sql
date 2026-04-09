
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.CurrencyRate.tabledata}}';


SET IDENTITY_INSERT [Sales].[CurrencyRate] ON;
MERGE INTO [Sales].[CurrencyRate] AS Target
USING (
  SELECT [AverageRate],[CurrencyRateDate],[CurrencyRateID],[EndOfDayRate],[FromCurrencyCode],[ModifiedDate],[ToCurrencyCode]
    FROM OPENJSON(@v_json)
    WITH (
           [AverageRate] MONEY,
           [CurrencyRateDate] DATETIME,
           [CurrencyRateID] INT,
           [EndOfDayRate] MONEY,
           [FromCurrencyCode] NCHAR(3),
           [ModifiedDate] DATETIME,
           [ToCurrencyCode] NCHAR(3)
    )
) AS Source
ON Source.[CurrencyRateID] = Target.[CurrencyRateID]

WHEN MATCHED AND (NOT (Target.[AverageRate] = Source.[AverageRate] OR (Target.[AverageRate] IS NULL AND Source.[AverageRate] IS NULL)) OR NOT (Target.[CurrencyRateDate] = Source.[CurrencyRateDate] OR (Target.[CurrencyRateDate] IS NULL AND Source.[CurrencyRateDate] IS NULL)) OR NOT (Target.[EndOfDayRate] = Source.[EndOfDayRate] OR (Target.[EndOfDayRate] IS NULL AND Source.[EndOfDayRate] IS NULL)) OR NOT (Target.[FromCurrencyCode] = Source.[FromCurrencyCode] OR (Target.[FromCurrencyCode] IS NULL AND Source.[FromCurrencyCode] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ToCurrencyCode] = Source.[ToCurrencyCode] OR (Target.[ToCurrencyCode] IS NULL AND Source.[ToCurrencyCode] IS NULL))) THEN
  UPDATE SET
        [AverageRate] = Source.[AverageRate],
        [CurrencyRateDate] = Source.[CurrencyRateDate],
        [EndOfDayRate] = Source.[EndOfDayRate],
        [FromCurrencyCode] = Source.[FromCurrencyCode],
        [ModifiedDate] = Source.[ModifiedDate],
        [ToCurrencyCode] = Source.[ToCurrencyCode]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AverageRate],
        [CurrencyRateDate],
        [CurrencyRateID],
        [EndOfDayRate],
        [FromCurrencyCode],
        [ModifiedDate],
        [ToCurrencyCode]
   ) VALUES (
         Source.[AverageRate],
        Source.[CurrencyRateDate],
        Source.[CurrencyRateID],
        Source.[EndOfDayRate],
        Source.[FromCurrencyCode],
        Source.[ModifiedDate],
        Source.[ToCurrencyCode]
   )
 ;
SET IDENTITY_INSERT [Sales].[CurrencyRate] OFF;
