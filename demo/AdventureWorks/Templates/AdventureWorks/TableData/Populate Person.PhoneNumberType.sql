
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2017-12-13T13:19:22.273","Name":"Cell","PhoneNumberTypeID":1},
{"ModifiedDate":"2017-12-13T13:19:22.273","Name":"Home","PhoneNumberTypeID":2},
{"ModifiedDate":"2017-12-13T13:19:22.273","Name":"Work","PhoneNumberTypeID":3}
]';

ALTER TABLE [Person].[PhoneNumberType] DISABLE TRIGGER ALL;
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


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
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
ALTER TABLE [Person].[PhoneNumberType] ENABLE TRIGGER ALL;
