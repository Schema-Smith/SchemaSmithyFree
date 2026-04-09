
DECLARE @v_json NVARCHAR(MAX) = '{{Person.BusinessEntityAddress.tabledata}}';



MERGE INTO [Person].[BusinessEntityAddress] AS Target
USING (
  SELECT [AddressID],[AddressTypeID],[BusinessEntityID],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [AddressID] INT,
           [AddressTypeID] INT,
           [BusinessEntityID] INT,
           [ModifiedDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[AddressID] = Target.[AddressID] AND Source.[AddressTypeID] = Target.[AddressTypeID] AND Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[AddressID] = Source.[AddressID] OR (Target.[AddressID] IS NULL AND Source.[AddressID] IS NULL)) OR NOT (Target.[AddressTypeID] = Source.[AddressTypeID] OR (Target.[AddressTypeID] IS NULL AND Source.[AddressTypeID] IS NULL)) OR NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [AddressID] = Source.[AddressID],
        [AddressTypeID] = Source.[AddressTypeID],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AddressID],
        [AddressTypeID],
        [BusinessEntityID],
        [ModifiedDate]
   ) VALUES (
         Source.[AddressID],
        Source.[AddressTypeID],
        Source.[BusinessEntityID],
        Source.[ModifiedDate]
   )
 ;
