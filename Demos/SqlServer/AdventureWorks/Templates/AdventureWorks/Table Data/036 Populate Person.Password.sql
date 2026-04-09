
DECLARE @v_json NVARCHAR(MAX) = '{{Person.Password.tabledata}}';



MERGE INTO [Person].[Password] AS Target
USING (
  SELECT [BusinessEntityID],[ModifiedDate],[PasswordHash],[PasswordSalt]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [ModifiedDate] DATETIME,
           [PasswordHash] VARCHAR(128),
           [PasswordSalt] VARCHAR(10),
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PasswordHash] = Source.[PasswordHash] OR (Target.[PasswordHash] IS NULL AND Source.[PasswordHash] IS NULL)) OR NOT (Target.[PasswordSalt] = Source.[PasswordSalt] OR (Target.[PasswordSalt] IS NULL AND Source.[PasswordSalt] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ModifiedDate] = Source.[ModifiedDate],
        [PasswordHash] = Source.[PasswordHash],
        [PasswordSalt] = Source.[PasswordSalt]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [ModifiedDate],
        [PasswordHash],
        [PasswordSalt]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[ModifiedDate],
        Source.[PasswordHash],
        Source.[PasswordSalt]
   )
 ;
