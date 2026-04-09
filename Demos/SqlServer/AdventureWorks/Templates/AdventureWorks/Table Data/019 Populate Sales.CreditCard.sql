
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.CreditCard.tabledata}}';


SET IDENTITY_INSERT [Sales].[CreditCard] ON;
MERGE INTO [Sales].[CreditCard] AS Target
USING (
  SELECT [CardNumber],[CardType],[CreditCardID],[ExpMonth],[ExpYear],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [CardNumber] NVARCHAR(25),
           [CardType] NVARCHAR(50),
           [CreditCardID] INT,
           [ExpMonth] TINYINT,
           [ExpYear] SMALLINT,
           [ModifiedDate] DATETIME
    )
) AS Source
ON Source.[CreditCardID] = Target.[CreditCardID]

WHEN MATCHED AND (NOT (Target.[CardNumber] = Source.[CardNumber] OR (Target.[CardNumber] IS NULL AND Source.[CardNumber] IS NULL)) OR NOT (Target.[CardType] = Source.[CardType] OR (Target.[CardType] IS NULL AND Source.[CardType] IS NULL)) OR NOT (Target.[ExpMonth] = Source.[ExpMonth] OR (Target.[ExpMonth] IS NULL AND Source.[ExpMonth] IS NULL)) OR NOT (Target.[ExpYear] = Source.[ExpYear] OR (Target.[ExpYear] IS NULL AND Source.[ExpYear] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [CardNumber] = Source.[CardNumber],
        [CardType] = Source.[CardType],
        [ExpMonth] = Source.[ExpMonth],
        [ExpYear] = Source.[ExpYear],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CardNumber],
        [CardType],
        [CreditCardID],
        [ExpMonth],
        [ExpYear],
        [ModifiedDate]
   ) VALUES (
         Source.[CardNumber],
        Source.[CardType],
        Source.[CreditCardID],
        Source.[ExpMonth],
        Source.[ExpYear],
        Source.[ModifiedDate]
   )
 ;
SET IDENTITY_INSERT [Sales].[CreditCard] OFF;
