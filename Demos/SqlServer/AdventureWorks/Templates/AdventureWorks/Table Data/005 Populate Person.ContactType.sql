
DECLARE @v_json NVARCHAR(MAX) = '{{Person.ContactType.tabledata}}';


SET IDENTITY_INSERT [Person].[ContactType] ON;
MERGE INTO [Person].[ContactType] AS Target
USING (
  SELECT [ContactTypeID],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [ContactTypeID] INT,
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[ContactTypeID] = Target.[ContactTypeID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ContactTypeID],
        [ModifiedDate],
        [Name]
   ) VALUES (
         Source.[ContactTypeID],
        Source.[ModifiedDate],
        Source.[Name]
   )
 ;
SET IDENTITY_INSERT [Person].[ContactType] OFF;
