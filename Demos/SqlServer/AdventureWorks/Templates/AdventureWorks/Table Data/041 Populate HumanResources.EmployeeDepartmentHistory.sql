
DECLARE @v_json NVARCHAR(MAX) = '{{HumanResources.EmployeeDepartmentHistory.tabledata}}';



MERGE INTO [HumanResources].[EmployeeDepartmentHistory] AS Target
USING (
  SELECT [BusinessEntityID],[DepartmentID],[EndDate],[ModifiedDate],[ShiftID],[StartDate]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [DepartmentID] SMALLINT,
           [EndDate] DATE,
           [ModifiedDate] DATETIME,
           [ShiftID] TINYINT,
           [StartDate] DATE
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID] AND Source.[DepartmentID] = Target.[DepartmentID] AND Source.[ShiftID] = Target.[ShiftID] AND Source.[StartDate] = Target.[StartDate]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[DepartmentID] = Source.[DepartmentID] OR (Target.[DepartmentID] IS NULL AND Source.[DepartmentID] IS NULL)) OR NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ShiftID] = Source.[ShiftID] OR (Target.[ShiftID] IS NULL AND Source.[ShiftID] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [DepartmentID] = Source.[DepartmentID],
        [EndDate] = Source.[EndDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [ShiftID] = Source.[ShiftID],
        [StartDate] = Source.[StartDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [DepartmentID],
        [EndDate],
        [ModifiedDate],
        [ShiftID],
        [StartDate]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[DepartmentID],
        Source.[EndDate],
        Source.[ModifiedDate],
        Source.[ShiftID],
        Source.[StartDate]
   )
 ;
