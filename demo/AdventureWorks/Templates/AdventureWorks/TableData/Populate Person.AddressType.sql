
DECLARE @v_json NVARCHAR(MAX) = '[
{"AddressTypeID":1,"ModifiedDate":"2008-04-30T00:00:00","Name":"Billing"},
{"AddressTypeID":2,"ModifiedDate":"2008-04-30T00:00:00","Name":"Home"},
{"AddressTypeID":3,"ModifiedDate":"2008-04-30T00:00:00","Name":"Main Office"},
{"AddressTypeID":4,"ModifiedDate":"2008-04-30T00:00:00","Name":"Primary"},
{"AddressTypeID":5,"ModifiedDate":"2008-04-30T00:00:00","Name":"Shipping"},
{"AddressTypeID":6,"ModifiedDate":"2008-04-30T00:00:00","Name":"Archive"}
]';

ALTER TABLE [Person].[AddressType] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [Person].[AddressType] ON; 
MERGE INTO [Person].[AddressType] AS Target
USING (
  SELECT [AddressTypeID],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [AddressTypeID] INT,
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[AddressTypeID] = Target.[AddressTypeID]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
  INSERT (
        [AddressTypeID],
        [ModifiedDate],
        [Name]
  ) VALUES (
        Source.[AddressTypeID],
        Source.[ModifiedDate],
        Source.[Name]  
  )
;
SET IDENTITY_INSERT [Person].[AddressType] OFF;
ALTER TABLE [Person].[AddressType] ENABLE TRIGGER ALL;
