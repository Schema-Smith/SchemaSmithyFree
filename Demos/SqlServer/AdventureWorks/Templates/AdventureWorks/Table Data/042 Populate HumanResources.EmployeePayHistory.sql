
DECLARE @v_json NVARCHAR(MAX) = '{{HumanResources.EmployeePayHistory.tabledata}}';



MERGE INTO [HumanResources].[EmployeePayHistory] AS Target
USING (
  SELECT [BusinessEntityID],[ModifiedDate],[PayFrequency],[Rate],[RateChangeDate]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [ModifiedDate] DATETIME,
           [PayFrequency] TINYINT,
           [Rate] MONEY,
           [RateChangeDate] DATETIME
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[RateChangeDate] = Target.[RateChangeDate]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PayFrequency] = Source.[PayFrequency] OR (Target.[PayFrequency] IS NULL AND Source.[PayFrequency] IS NULL)) OR NOT (Target.[Rate] = Source.[Rate] OR (Target.[Rate] IS NULL AND Source.[Rate] IS NULL)) OR NOT (Target.[RateChangeDate] = Source.[RateChangeDate] OR (Target.[RateChangeDate] IS NULL AND Source.[RateChangeDate] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ModifiedDate] = Source.[ModifiedDate],
        [PayFrequency] = Source.[PayFrequency],
        [Rate] = Source.[Rate],
        [RateChangeDate] = Source.[RateChangeDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [ModifiedDate],
        [PayFrequency],
        [Rate],
        [RateChangeDate]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[ModifiedDate],
        Source.[PayFrequency],
        Source.[Rate],
        Source.[RateChangeDate]
   )
 ;
