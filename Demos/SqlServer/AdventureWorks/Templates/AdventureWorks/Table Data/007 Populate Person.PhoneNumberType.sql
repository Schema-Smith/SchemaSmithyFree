
DECLARE @v_json NVARCHAR(MAX) = '{{Person.PhoneNumberType.tabledata}}';


SET IDENTITY_INSERT [Person].[PhoneNumberType] ON;
MERGE INTO [Person].[PhoneNumberType] AS Target
USING (
  SELECT [ModifiedDate],[Name],[PhoneNumberTypeID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [PhoneNumberTypeID] INT
    )
) AS Source
ON Source.[PhoneNumberTypeID] = Target.[PhoneNumberTypeID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [PhoneNumberTypeID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[PhoneNumberTypeID]
   )
 ;
SET IDENTITY_INSERT [Person].[PhoneNumberType] OFF;
