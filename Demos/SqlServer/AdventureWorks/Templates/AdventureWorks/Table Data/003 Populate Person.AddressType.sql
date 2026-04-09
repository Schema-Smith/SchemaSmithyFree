
DECLARE @v_json NVARCHAR(MAX) = '{{Person.AddressType.tabledata}}';


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

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
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
