
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.PersonCreditCard.tabledata}}';



MERGE INTO [Sales].[PersonCreditCard] AS Target
USING (
  SELECT [BusinessEntityID],[CreditCardID],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [CreditCardID] INT,
           [ModifiedDate] DATETIME
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[CreditCardID] = Target.[CreditCardID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[CreditCardID] = Source.[CreditCardID] OR (Target.[CreditCardID] IS NULL AND Source.[CreditCardID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [CreditCardID] = Source.[CreditCardID],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [CreditCardID],
        [ModifiedDate]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[CreditCardID],
        Source.[ModifiedDate]
   )
 ;
