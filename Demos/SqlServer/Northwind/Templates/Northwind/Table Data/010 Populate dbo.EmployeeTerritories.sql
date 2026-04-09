
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.EmployeeTerritories.tabledata}}';



MERGE INTO [dbo].[EmployeeTerritories] AS Target
USING (
  SELECT [EmployeeID],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [EmployeeID] INT,
           [TerritoryID] NVARCHAR(20)
    )
) AS Source
ON Source.[EmployeeID] = Target.[EmployeeID] AND Source.[TerritoryID] = Target.[TerritoryID]

WHEN MATCHED AND (NOT (Target.[EmployeeID] = Source.[EmployeeID] OR (Target.[EmployeeID] IS NULL AND Source.[EmployeeID] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [EmployeeID] = Source.[EmployeeID],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [EmployeeID],
        [TerritoryID]
   ) VALUES (
         Source.[EmployeeID],
        Source.[TerritoryID]
   )
 ;
