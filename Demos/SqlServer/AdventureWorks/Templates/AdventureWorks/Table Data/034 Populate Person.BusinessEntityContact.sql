
DECLARE @v_json NVARCHAR(MAX) = '{{Person.BusinessEntityContact.tabledata}}';



MERGE INTO [Person].[BusinessEntityContact] AS Target
USING (
  SELECT [BusinessEntityID],[ContactTypeID],[ModifiedDate],[PersonID]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [ContactTypeID] INT,
           [ModifiedDate] DATETIME,
           [PersonID] INT,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[ContactTypeID] = Target.[ContactTypeID] AND Source.[PersonID] = Target.[PersonID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ContactTypeID] = Source.[ContactTypeID] OR (Target.[ContactTypeID] IS NULL AND Source.[ContactTypeID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PersonID] = Source.[PersonID] OR (Target.[PersonID] IS NULL AND Source.[PersonID] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ContactTypeID] = Source.[ContactTypeID],
        [ModifiedDate] = Source.[ModifiedDate],
        [PersonID] = Source.[PersonID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [ContactTypeID],
        [ModifiedDate],
        [PersonID]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[ContactTypeID],
        Source.[ModifiedDate],
        Source.[PersonID]
   )
 ;
