
DECLARE @v_json NVARCHAR(MAX) = '[
{"DepartmentID":1,"GroupName":"Research and Development","ModifiedDate":"2008-04-30T00:00:00","Name":"Engineering"},
{"DepartmentID":2,"GroupName":"Research and Development","ModifiedDate":"2008-04-30T00:00:00","Name":"Tool Design"},
{"DepartmentID":3,"GroupName":"Sales and Marketing","ModifiedDate":"2008-04-30T00:00:00","Name":"Sales"},
{"DepartmentID":4,"GroupName":"Sales and Marketing","ModifiedDate":"2008-04-30T00:00:00","Name":"Marketing"},
{"DepartmentID":5,"GroupName":"Inventory Management","ModifiedDate":"2008-04-30T00:00:00","Name":"Purchasing"},
{"DepartmentID":6,"GroupName":"Research and Development","ModifiedDate":"2008-04-30T00:00:00","Name":"Research and Development"},
{"DepartmentID":7,"GroupName":"Manufacturing","ModifiedDate":"2008-04-30T00:00:00","Name":"Production"},
{"DepartmentID":8,"GroupName":"Manufacturing","ModifiedDate":"2008-04-30T00:00:00","Name":"Production Control"},
{"DepartmentID":9,"GroupName":"Executive General and Administration","ModifiedDate":"2008-04-30T00:00:00","Name":"Human Resources"},
{"DepartmentID":10,"GroupName":"Executive General and Administration","ModifiedDate":"2008-04-30T00:00:00","Name":"Finance"},
{"DepartmentID":11,"GroupName":"Executive General and Administration","ModifiedDate":"2008-04-30T00:00:00","Name":"Information Services"},
{"DepartmentID":12,"GroupName":"Quality Assurance","ModifiedDate":"2008-04-30T00:00:00","Name":"Document Control"},
{"DepartmentID":13,"GroupName":"Quality Assurance","ModifiedDate":"2008-04-30T00:00:00","Name":"Quality Assurance"},
{"DepartmentID":14,"GroupName":"Executive General and Administration","ModifiedDate":"2008-04-30T00:00:00","Name":"Facilities and Maintenance"},
{"DepartmentID":15,"GroupName":"Inventory Management","ModifiedDate":"2008-04-30T00:00:00","Name":"Shipping and Receiving"},
{"DepartmentID":16,"GroupName":"Executive General and Administration","ModifiedDate":"2008-04-30T00:00:00","Name":"Executive"}
]';

ALTER TABLE [HumanResources].[Department] DISABLE TRIGGER ALL;
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


WHEN MATCHED AND (NOT (Target.[GroupName] = Source.[GroupName] OR (Target.[GroupName] IS NULL AND Source.[GroupName] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [GroupName] = Source.[GroupName],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
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
ALTER TABLE [HumanResources].[Department] ENABLE TRIGGER ALL;
