
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.Currency.tabledata}}';



MERGE INTO [Sales].[Currency] AS Target
USING (
  SELECT [CurrencyCode],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [CurrencyCode] NCHAR(3),
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[CurrencyCode] = Target.[CurrencyCode]

WHEN MATCHED AND (NOT (Target.[CurrencyCode] = Source.[CurrencyCode] OR (Target.[CurrencyCode] IS NULL AND Source.[CurrencyCode] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [CurrencyCode] = Source.[CurrencyCode],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CurrencyCode],
        [ModifiedDate],
        [Name]
   ) VALUES (
         Source.[CurrencyCode],
        Source.[ModifiedDate],
        Source.[Name]
   )
 ;
