
DECLARE @v_json NVARCHAR(MAX) = '{{HumanResources.Department.tabledata}}';


SET IDENTITY_INSERT [HumanResources].[Department] ON;
MERGE INTO [HumanResources].[Department] AS Target
USING (
  SELECT [DepartmentID],[GroupName],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [DepartmentID] SMALLINT,
           [GroupName] NAME,
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[DepartmentID] = Target.[DepartmentID]

WHEN MATCHED AND (NOT (Target.[GroupName] = Source.[GroupName] OR (Target.[GroupName] IS NULL AND Source.[GroupName] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [GroupName] = Source.[GroupName],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [DepartmentID],
        [GroupName],
        [ModifiedDate],
        [Name]
   ) VALUES (
         Source.[DepartmentID],
        Source.[GroupName],
        Source.[ModifiedDate],
        Source.[Name]
   )
 ;
SET IDENTITY_INSERT [HumanResources].[Department] OFF;
