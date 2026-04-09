
DECLARE @v_json NVARCHAR(MAX) = '{{Person.PersonPhone.tabledata}}';



MERGE INTO [Person].[PersonPhone] AS Target
USING (
  SELECT [BusinessEntityID],[ModifiedDate],[PhoneNumber],[PhoneNumberTypeID]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [ModifiedDate] DATETIME,
           [PhoneNumber] PHONE,
           [PhoneNumberTypeID] INT
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[PhoneNumber] = Target.[PhoneNumber] AND Source.[PhoneNumberTypeID] = Target.[PhoneNumberTypeID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PhoneNumber] = Source.[PhoneNumber] OR (Target.[PhoneNumber] IS NULL AND Source.[PhoneNumber] IS NULL)) OR NOT (Target.[PhoneNumberTypeID] = Source.[PhoneNumberTypeID] OR (Target.[PhoneNumberTypeID] IS NULL AND Source.[PhoneNumberTypeID] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ModifiedDate] = Source.[ModifiedDate],
        [PhoneNumber] = Source.[PhoneNumber],
        [PhoneNumberTypeID] = Source.[PhoneNumberTypeID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [ModifiedDate],
        [PhoneNumber],
        [PhoneNumberTypeID]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[ModifiedDate],
        Source.[PhoneNumber],
        Source.[PhoneNumberTypeID]
   )
 ;
