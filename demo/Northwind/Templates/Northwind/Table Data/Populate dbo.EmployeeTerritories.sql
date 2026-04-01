
DECLARE @v_json NVARCHAR(MAX) = '[
{"EmployeeID":1,"TerritoryID":"06897"},
{"EmployeeID":1,"TerritoryID":"19713"},
{"EmployeeID":2,"TerritoryID":"01581"},
{"EmployeeID":2,"TerritoryID":"01730"},
{"EmployeeID":2,"TerritoryID":"01833"},
{"EmployeeID":2,"TerritoryID":"02116"},
{"EmployeeID":2,"TerritoryID":"02139"},
{"EmployeeID":2,"TerritoryID":"02184"},
{"EmployeeID":2,"TerritoryID":"40222"},
{"EmployeeID":3,"TerritoryID":"30346"},
{"EmployeeID":3,"TerritoryID":"31406"},
{"EmployeeID":3,"TerritoryID":"32859"},
{"EmployeeID":3,"TerritoryID":"33607"},
{"EmployeeID":4,"TerritoryID":"20852"},
{"EmployeeID":4,"TerritoryID":"27403"},
{"EmployeeID":4,"TerritoryID":"27511"},
{"EmployeeID":5,"TerritoryID":"02903"},
{"EmployeeID":5,"TerritoryID":"07960"},
{"EmployeeID":5,"TerritoryID":"08837"},
{"EmployeeID":5,"TerritoryID":"10019"},
{"EmployeeID":5,"TerritoryID":"10038"},
{"EmployeeID":5,"TerritoryID":"11747"},
{"EmployeeID":5,"TerritoryID":"14450"},
{"EmployeeID":6,"TerritoryID":"85014"},
{"EmployeeID":6,"TerritoryID":"85251"},
{"EmployeeID":6,"TerritoryID":"98004"},
{"EmployeeID":6,"TerritoryID":"98052"},
{"EmployeeID":6,"TerritoryID":"98104"},
{"EmployeeID":7,"TerritoryID":"60179"},
{"EmployeeID":7,"TerritoryID":"60601"},
{"EmployeeID":7,"TerritoryID":"80202"},
{"EmployeeID":7,"TerritoryID":"80909"},
{"EmployeeID":7,"TerritoryID":"90405"},
{"EmployeeID":7,"TerritoryID":"94025"},
{"EmployeeID":7,"TerritoryID":"94105"},
{"EmployeeID":7,"TerritoryID":"95008"},
{"EmployeeID":7,"TerritoryID":"95054"},
{"EmployeeID":7,"TerritoryID":"95060"},
{"EmployeeID":8,"TerritoryID":"19428"},
{"EmployeeID":8,"TerritoryID":"44122"},
{"EmployeeID":8,"TerritoryID":"45839"},
{"EmployeeID":8,"TerritoryID":"53404"},
{"EmployeeID":9,"TerritoryID":"03049"},
{"EmployeeID":9,"TerritoryID":"03801"},
{"EmployeeID":9,"TerritoryID":"48075"},
{"EmployeeID":9,"TerritoryID":"48084"},
{"EmployeeID":9,"TerritoryID":"48304"},
{"EmployeeID":9,"TerritoryID":"55113"},
{"EmployeeID":9,"TerritoryID":"55439"}
]';

ALTER TABLE [dbo].[EmployeeTerritories] DISABLE TRIGGER ALL;
 
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


WHEN MATCHED AND (NOT (Target.[EmployeeID] = Source.[EmployeeID] OR (Target.[EmployeeID] IS NULL AND Source.[EmployeeID] IS NULL)) AND NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [EmployeeID] = Source.[EmployeeID],
        [TerritoryID] = Source.[TerritoryID]


WHEN NOT MATCHED THEN
  INSERT (
        [EmployeeID],
        [TerritoryID]
  ) VALUES (
        Source.[EmployeeID],
        Source.[TerritoryID]  
  )
;
ALTER TABLE [dbo].[EmployeeTerritories] ENABLE TRIGGER ALL;
