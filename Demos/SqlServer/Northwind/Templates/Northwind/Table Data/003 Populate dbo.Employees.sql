
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Employees.tabledata}}';


SET IDENTITY_INSERT [dbo].[Employees] ON;
MERGE INTO [dbo].[Employees] AS Target
USING (
  SELECT [Address],[BirthDate],[City],[Country],[EmployeeID],[Extension],[FirstName],[HireDate],[HomePhone],[LastName],[Notes],[Photo],[PhotoPath],[PostalCode],[Region],[ReportsTo],[Title],[TitleOfCourtesy]
    FROM OPENJSON(@v_json)
    WITH (
           [Address] NVARCHAR(60),
           [BirthDate] DATETIME,
           [City] NVARCHAR(15),
           [Country] NVARCHAR(15),
           [EmployeeID] INT,
           [Extension] NVARCHAR(4),
           [FirstName] NVARCHAR(10),
           [HireDate] DATETIME,
           [HomePhone] NVARCHAR(24),
           [LastName] NVARCHAR(20),
           [Notes] NVARCHAR(MAX),
           [Photo] VARBINARY(MAX),
           [PhotoPath] NVARCHAR(255),
           [PostalCode] NVARCHAR(10),
           [Region] NVARCHAR(15),
           [ReportsTo] INT,
           [Title] NVARCHAR(30),
           [TitleOfCourtesy] NVARCHAR(25)
    )
) AS Source
ON Source.[EmployeeID] = Target.[EmployeeID]

WHEN MATCHED AND (NOT (Target.[Address] = Source.[Address] OR (Target.[Address] IS NULL AND Source.[Address] IS NULL)) OR NOT (Target.[BirthDate] = Source.[BirthDate] OR (Target.[BirthDate] IS NULL AND Source.[BirthDate] IS NULL)) OR NOT (Target.[City] = Source.[City] OR (Target.[City] IS NULL AND Source.[City] IS NULL)) OR NOT (Target.[Country] = Source.[Country] OR (Target.[Country] IS NULL AND Source.[Country] IS NULL)) OR NOT (Target.[Extension] = Source.[Extension] OR (Target.[Extension] IS NULL AND Source.[Extension] IS NULL)) OR NOT (Target.[FirstName] = Source.[FirstName] OR (Target.[FirstName] IS NULL AND Source.[FirstName] IS NULL)) OR NOT (Target.[HireDate] = Source.[HireDate] OR (Target.[HireDate] IS NULL AND Source.[HireDate] IS NULL)) OR NOT (Target.[HomePhone] = Source.[HomePhone] OR (Target.[HomePhone] IS NULL AND Source.[HomePhone] IS NULL)) OR NOT (Target.[LastName] = Source.[LastName] OR (Target.[LastName] IS NULL AND Source.[LastName] IS NULL)) OR NOT (CAST(Target.[Notes] AS NVARCHAR(MAX)) = CAST(Source.[Notes] AS NVARCHAR(MAX)) OR (Target.[Notes] IS NULL AND Source.[Notes] IS NULL)) OR NOT (CAST(Target.[Photo] AS VARBINARY(MAX)) = CAST(Source.[Photo] AS VARBINARY(MAX)) OR (Target.[Photo] IS NULL AND Source.[Photo] IS NULL)) OR NOT (Target.[PhotoPath] = Source.[PhotoPath] OR (Target.[PhotoPath] IS NULL AND Source.[PhotoPath] IS NULL)) OR NOT (Target.[PostalCode] = Source.[PostalCode] OR (Target.[PostalCode] IS NULL AND Source.[PostalCode] IS NULL)) OR NOT (Target.[Region] = Source.[Region] OR (Target.[Region] IS NULL AND Source.[Region] IS NULL)) OR NOT (Target.[ReportsTo] = Source.[ReportsTo] OR (Target.[ReportsTo] IS NULL AND Source.[ReportsTo] IS NULL)) OR NOT (Target.[Title] = Source.[Title] OR (Target.[Title] IS NULL AND Source.[Title] IS NULL)) OR NOT (Target.[TitleOfCourtesy] = Source.[TitleOfCourtesy] OR (Target.[TitleOfCourtesy] IS NULL AND Source.[TitleOfCourtesy] IS NULL))) THEN
  UPDATE SET
        [Address] = Source.[Address],
        [BirthDate] = Source.[BirthDate],
        [City] = Source.[City],
        [Country] = Source.[Country],
        [Extension] = Source.[Extension],
        [FirstName] = Source.[FirstName],
        [HireDate] = Source.[HireDate],
        [HomePhone] = Source.[HomePhone],
        [LastName] = Source.[LastName],
        [Notes] = Source.[Notes],
        [Photo] = Source.[Photo],
        [PhotoPath] = Source.[PhotoPath],
        [PostalCode] = Source.[PostalCode],
        [Region] = Source.[Region],
        [ReportsTo] = Source.[ReportsTo],
        [Title] = Source.[Title],
        [TitleOfCourtesy] = Source.[TitleOfCourtesy]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Address],
        [BirthDate],
        [City],
        [Country],
        [EmployeeID],
        [Extension],
        [FirstName],
        [HireDate],
        [HomePhone],
        [LastName],
        [Notes],
        [Photo],
        [PhotoPath],
        [PostalCode],
        [Region],
        [ReportsTo],
        [Title],
        [TitleOfCourtesy]
   ) VALUES (
         Source.[Address],
        Source.[BirthDate],
        Source.[City],
        Source.[Country],
        Source.[EmployeeID],
        Source.[Extension],
        Source.[FirstName],
        Source.[HireDate],
        Source.[HomePhone],
        Source.[LastName],
        Source.[Notes],
        Source.[Photo],
        Source.[PhotoPath],
        Source.[PostalCode],
        Source.[Region],
        Source.[ReportsTo],
        Source.[Title],
        Source.[TitleOfCourtesy]
   )
 ;
SET IDENTITY_INSERT [dbo].[Employees] OFF;
