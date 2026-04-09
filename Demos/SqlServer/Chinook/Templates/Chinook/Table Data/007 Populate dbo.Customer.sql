
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Customer.tabledata}}';


SET IDENTITY_INSERT [dbo].[Customer] ON;
MERGE INTO [dbo].[Customer] AS Target
USING (
  SELECT [Address],[City],[Company],[Country],[CustomerId],[Email],[Fax],[FirstName],[LastName],[Phone],[PostalCode],[State],[SupportRepId]
    FROM OPENJSON(@v_json)
    WITH (
           [Address] NVARCHAR(70),
           [City] NVARCHAR(40),
           [Company] NVARCHAR(80),
           [Country] NVARCHAR(40),
           [CustomerId] INT,
           [Email] NVARCHAR(60),
           [Fax] NVARCHAR(24),
           [FirstName] NVARCHAR(40),
           [LastName] NVARCHAR(20),
           [Phone] NVARCHAR(24),
           [PostalCode] NVARCHAR(10),
           [State] NVARCHAR(40),
           [SupportRepId] INT
    )
) AS Source
ON Source.[CustomerId] = Target.[CustomerId]

WHEN MATCHED AND (NOT (Target.[Address] = Source.[Address] OR (Target.[Address] IS NULL AND Source.[Address] IS NULL)) OR NOT (Target.[City] = Source.[City] OR (Target.[City] IS NULL AND Source.[City] IS NULL)) OR NOT (Target.[Company] = Source.[Company] OR (Target.[Company] IS NULL AND Source.[Company] IS NULL)) OR NOT (Target.[Country] = Source.[Country] OR (Target.[Country] IS NULL AND Source.[Country] IS NULL)) OR NOT (Target.[Email] = Source.[Email] OR (Target.[Email] IS NULL AND Source.[Email] IS NULL)) OR NOT (Target.[Fax] = Source.[Fax] OR (Target.[Fax] IS NULL AND Source.[Fax] IS NULL)) OR NOT (Target.[FirstName] = Source.[FirstName] OR (Target.[FirstName] IS NULL AND Source.[FirstName] IS NULL)) OR NOT (Target.[LastName] = Source.[LastName] OR (Target.[LastName] IS NULL AND Source.[LastName] IS NULL)) OR NOT (Target.[Phone] = Source.[Phone] OR (Target.[Phone] IS NULL AND Source.[Phone] IS NULL)) OR NOT (Target.[PostalCode] = Source.[PostalCode] OR (Target.[PostalCode] IS NULL AND Source.[PostalCode] IS NULL)) OR NOT (Target.[State] = Source.[State] OR (Target.[State] IS NULL AND Source.[State] IS NULL)) OR NOT (Target.[SupportRepId] = Source.[SupportRepId] OR (Target.[SupportRepId] IS NULL AND Source.[SupportRepId] IS NULL))) THEN
  UPDATE SET
        [Address] = Source.[Address],
        [City] = Source.[City],
        [Company] = Source.[Company],
        [Country] = Source.[Country],
        [Email] = Source.[Email],
        [Fax] = Source.[Fax],
        [FirstName] = Source.[FirstName],
        [LastName] = Source.[LastName],
        [Phone] = Source.[Phone],
        [PostalCode] = Source.[PostalCode],
        [State] = Source.[State],
        [SupportRepId] = Source.[SupportRepId]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Address],
        [City],
        [Company],
        [Country],
        [CustomerId],
        [Email],
        [Fax],
        [FirstName],
        [LastName],
        [Phone],
        [PostalCode],
        [State],
        [SupportRepId]
   ) VALUES (
         Source.[Address],
        Source.[City],
        Source.[Company],
        Source.[Country],
        Source.[CustomerId],
        Source.[Email],
        Source.[Fax],
        Source.[FirstName],
        Source.[LastName],
        Source.[Phone],
        Source.[PostalCode],
        Source.[State],
        Source.[SupportRepId]
   )
 ;
SET IDENTITY_INSERT [dbo].[Customer] OFF;
