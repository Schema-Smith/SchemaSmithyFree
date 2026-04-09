
DECLARE @v_json NVARCHAR(MAX) = '{{Person.BusinessEntity.tabledata}}';


SET IDENTITY_INSERT [Person].[BusinessEntity] ON;
MERGE INTO [Person].[BusinessEntity] AS Target
USING (
  SELECT [BusinessEntityID],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [ModifiedDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [ModifiedDate]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[ModifiedDate]
   )
 ;
SET IDENTITY_INSERT [Person].[BusinessEntity] OFF;
