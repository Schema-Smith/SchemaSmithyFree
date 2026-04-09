
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Employee.tabledata}}';


SET IDENTITY_INSERT [dbo].[Employee] ON;
MERGE INTO [dbo].[Employee] AS Target
USING (
  SELECT [Address],[BirthDate],[City],[Country],[Email],[EmployeeId],[Fax],[FirstName],[HireDate],[LastName],[Phone],[PostalCode],[ReportsTo],[State],[Title]
    FROM OPENJSON(@v_json)
    WITH (
           [Address] NVARCHAR(70),
           [BirthDate] DATETIME,
           [City] NVARCHAR(40),
           [Country] NVARCHAR(40),
           [Email] NVARCHAR(60),
           [EmployeeId] INT,
           [Fax] NVARCHAR(24),
           [FirstName] NVARCHAR(20),
           [HireDate] DATETIME,
           [LastName] NVARCHAR(20),
           [Phone] NVARCHAR(24),
           [PostalCode] NVARCHAR(10),
           [ReportsTo] INT,
           [State] NVARCHAR(40),
           [Title] NVARCHAR(30)
    )
) AS Source
ON Source.[EmployeeId] = Target.[EmployeeId]

WHEN MATCHED AND (NOT (Target.[Address] = Source.[Address] OR (Target.[Address] IS NULL AND Source.[Address] IS NULL)) OR NOT (Target.[BirthDate] = Source.[BirthDate] OR (Target.[BirthDate] IS NULL AND Source.[BirthDate] IS NULL)) OR NOT (Target.[City] = Source.[City] OR (Target.[City] IS NULL AND Source.[City] IS NULL)) OR NOT (Target.[Country] = Source.[Country] OR (Target.[Country] IS NULL AND Source.[Country] IS NULL)) OR NOT (Target.[Email] = Source.[Email] OR (Target.[Email] IS NULL AND Source.[Email] IS NULL)) OR NOT (Target.[Fax] = Source.[Fax] OR (Target.[Fax] IS NULL AND Source.[Fax] IS NULL)) OR NOT (Target.[FirstName] = Source.[FirstName] OR (Target.[FirstName] IS NULL AND Source.[FirstName] IS NULL)) OR NOT (Target.[HireDate] = Source.[HireDate] OR (Target.[HireDate] IS NULL AND Source.[HireDate] IS NULL)) OR NOT (Target.[LastName] = Source.[LastName] OR (Target.[LastName] IS NULL AND Source.[LastName] IS NULL)) OR NOT (Target.[Phone] = Source.[Phone] OR (Target.[Phone] IS NULL AND Source.[Phone] IS NULL)) OR NOT (Target.[PostalCode] = Source.[PostalCode] OR (Target.[PostalCode] IS NULL AND Source.[PostalCode] IS NULL)) OR NOT (Target.[ReportsTo] = Source.[ReportsTo] OR (Target.[ReportsTo] IS NULL AND Source.[ReportsTo] IS NULL)) OR NOT (Target.[State] = Source.[State] OR (Target.[State] IS NULL AND Source.[State] IS NULL)) OR NOT (Target.[Title] = Source.[Title] OR (Target.[Title] IS NULL AND Source.[Title] IS NULL))) THEN
  UPDATE SET
        [Address] = Source.[Address],
        [BirthDate] = Source.[BirthDate],
        [City] = Source.[City],
        [Country] = Source.[Country],
        [Email] = Source.[Email],
        [Fax] = Source.[Fax],
        [FirstName] = Source.[FirstName],
        [HireDate] = Source.[HireDate],
        [LastName] = Source.[LastName],
        [Phone] = Source.[Phone],
        [PostalCode] = Source.[PostalCode],
        [ReportsTo] = Source.[ReportsTo],
        [State] = Source.[State],
        [Title] = Source.[Title]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Address],
        [BirthDate],
        [City],
        [Country],
        [Email],
        [EmployeeId],
        [Fax],
        [FirstName],
        [HireDate],
        [LastName],
        [Phone],
        [PostalCode],
        [ReportsTo],
        [State],
        [Title]
   ) VALUES (
         Source.[Address],
        Source.[BirthDate],
        Source.[City],
        Source.[Country],
        Source.[Email],
        Source.[EmployeeId],
        Source.[Fax],
        Source.[FirstName],
        Source.[HireDate],
        Source.[LastName],
        Source.[Phone],
        Source.[PostalCode],
        Source.[ReportsTo],
        Source.[State],
        Source.[Title]
   )
 ;
SET IDENTITY_INSERT [dbo].[Employee] OFF;
