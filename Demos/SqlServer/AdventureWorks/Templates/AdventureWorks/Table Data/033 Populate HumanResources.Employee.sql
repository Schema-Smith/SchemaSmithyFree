
DECLARE @v_json NVARCHAR(MAX) = '{{HumanResources.Employee.tabledata}}';



MERGE INTO [HumanResources].[Employee] AS Target
USING (
  SELECT [BirthDate],[BusinessEntityID],[CurrentFlag],[Gender],[HireDate],[JobTitle],[LoginID],[MaritalStatus],[ModifiedDate],[NationalIDNumber],[OrganizationNode],[SalariedFlag],[SickLeaveHours],[VacationHours]
    FROM OPENJSON(@v_json)
    WITH (
           [BirthDate] DATE,
           [BusinessEntityID] INT,
           [CurrentFlag] FLAG,
           [Gender] NCHAR(1),
           [HireDate] DATE,
           [JobTitle] NVARCHAR(50),
           [LoginID] NVARCHAR(256),
           [MaritalStatus] NCHAR(1),
           [ModifiedDate] DATETIME,
           [NationalIDNumber] NVARCHAR(15),
           [OrganizationNode] NVARCHAR(4000),
           [rowguid] UNIQUEIDENTIFIER,
           [SalariedFlag] FLAG,
           [SickLeaveHours] SMALLINT,
           [VacationHours] SMALLINT
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[BirthDate] = Source.[BirthDate] OR (Target.[BirthDate] IS NULL AND Source.[BirthDate] IS NULL)) OR NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[CurrentFlag] = Source.[CurrentFlag] OR (Target.[CurrentFlag] IS NULL AND Source.[CurrentFlag] IS NULL)) OR NOT (Target.[Gender] = Source.[Gender] OR (Target.[Gender] IS NULL AND Source.[Gender] IS NULL)) OR NOT (Target.[HireDate] = Source.[HireDate] OR (Target.[HireDate] IS NULL AND Source.[HireDate] IS NULL)) OR NOT (Target.[JobTitle] = Source.[JobTitle] OR (Target.[JobTitle] IS NULL AND Source.[JobTitle] IS NULL)) OR NOT (Target.[LoginID] = Source.[LoginID] OR (Target.[LoginID] IS NULL AND Source.[LoginID] IS NULL)) OR NOT (Target.[MaritalStatus] = Source.[MaritalStatus] OR (Target.[MaritalStatus] IS NULL AND Source.[MaritalStatus] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[NationalIDNumber] = Source.[NationalIDNumber] OR (Target.[NationalIDNumber] IS NULL AND Source.[NationalIDNumber] IS NULL)) OR NOT (Target.[OrganizationNode] = Source.[OrganizationNode] OR (Target.[OrganizationNode] IS NULL AND Source.[OrganizationNode] IS NULL)) OR NOT (Target.[SalariedFlag] = Source.[SalariedFlag] OR (Target.[SalariedFlag] IS NULL AND Source.[SalariedFlag] IS NULL)) OR NOT (Target.[SickLeaveHours] = Source.[SickLeaveHours] OR (Target.[SickLeaveHours] IS NULL AND Source.[SickLeaveHours] IS NULL)) OR NOT (Target.[VacationHours] = Source.[VacationHours] OR (Target.[VacationHours] IS NULL AND Source.[VacationHours] IS NULL))) THEN
  UPDATE SET
        [BirthDate] = Source.[BirthDate],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [CurrentFlag] = Source.[CurrentFlag],
        [Gender] = Source.[Gender],
        [HireDate] = Source.[HireDate],
        [JobTitle] = Source.[JobTitle],
        [LoginID] = Source.[LoginID],
        [MaritalStatus] = Source.[MaritalStatus],
        [ModifiedDate] = Source.[ModifiedDate],
        [NationalIDNumber] = Source.[NationalIDNumber],
        [OrganizationNode] = Source.[OrganizationNode],
        [SalariedFlag] = Source.[SalariedFlag],
        [SickLeaveHours] = Source.[SickLeaveHours],
        [VacationHours] = Source.[VacationHours]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BirthDate],
        [BusinessEntityID],
        [CurrentFlag],
        [Gender],
        [HireDate],
        [JobTitle],
        [LoginID],
        [MaritalStatus],
        [ModifiedDate],
        [NationalIDNumber],
        [OrganizationNode],
        [SalariedFlag],
        [SickLeaveHours],
        [VacationHours]
   ) VALUES (
         Source.[BirthDate],
        Source.[BusinessEntityID],
        Source.[CurrentFlag],
        Source.[Gender],
        Source.[HireDate],
        Source.[JobTitle],
        Source.[LoginID],
        Source.[MaritalStatus],
        Source.[ModifiedDate],
        Source.[NationalIDNumber],
        Source.[OrganizationNode],
        Source.[SalariedFlag],
        Source.[SickLeaveHours],
        Source.[VacationHours]
   )
 ;
