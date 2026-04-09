
DECLARE @v_json NVARCHAR(MAX) = '{{Person.EmailAddress.tabledata}}';


SET IDENTITY_INSERT [Person].[EmailAddress] ON;
MERGE INTO [Person].[EmailAddress] AS Target
USING (
  SELECT [BusinessEntityID],[EmailAddress],[EmailAddressID],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [EmailAddress] NVARCHAR(50),
           [EmailAddressID] INT,
           [ModifiedDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[EmailAddressID] = Target.[EmailAddressID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[EmailAddress] = Source.[EmailAddress] OR (Target.[EmailAddress] IS NULL AND Source.[EmailAddress] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [EmailAddress] = Source.[EmailAddress],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [EmailAddress],
        [EmailAddressID],
        [ModifiedDate]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[EmailAddress],
        Source.[EmailAddressID],
        Source.[ModifiedDate]
   )
 ;
SET IDENTITY_INSERT [Person].[EmailAddress] OFF;
