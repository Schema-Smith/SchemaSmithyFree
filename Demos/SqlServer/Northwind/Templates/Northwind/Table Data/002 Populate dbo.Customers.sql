
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Customers.tabledata}}';



MERGE INTO [dbo].[Customers] AS Target
USING (
  SELECT [Address],[City],[CompanyName],[ContactName],[ContactTitle],[Country],[CustomerID],[Fax],[Phone],[PostalCode],[Region]
    FROM OPENJSON(@v_json)
    WITH (
           [Address] NVARCHAR(60),
           [City] NVARCHAR(15),
           [CompanyName] NVARCHAR(40),
           [ContactName] NVARCHAR(30),
           [ContactTitle] NVARCHAR(30),
           [Country] NVARCHAR(15),
           [CustomerID] NCHAR(5),
           [Fax] NVARCHAR(24),
           [Phone] NVARCHAR(24),
           [PostalCode] NVARCHAR(10),
           [Region] NVARCHAR(15)
    )
) AS Source
ON Source.[CustomerID] = Target.[CustomerID]

WHEN MATCHED AND (NOT (Target.[Address] = Source.[Address] OR (Target.[Address] IS NULL AND Source.[Address] IS NULL)) OR NOT (Target.[City] = Source.[City] OR (Target.[City] IS NULL AND Source.[City] IS NULL)) OR NOT (Target.[CompanyName] = Source.[CompanyName] OR (Target.[CompanyName] IS NULL AND Source.[CompanyName] IS NULL)) OR NOT (Target.[ContactName] = Source.[ContactName] OR (Target.[ContactName] IS NULL AND Source.[ContactName] IS NULL)) OR NOT (Target.[ContactTitle] = Source.[ContactTitle] OR (Target.[ContactTitle] IS NULL AND Source.[ContactTitle] IS NULL)) OR NOT (Target.[Country] = Source.[Country] OR (Target.[Country] IS NULL AND Source.[Country] IS NULL)) OR NOT (Target.[CustomerID] = Source.[CustomerID] OR (Target.[CustomerID] IS NULL AND Source.[CustomerID] IS NULL)) OR NOT (Target.[Fax] = Source.[Fax] OR (Target.[Fax] IS NULL AND Source.[Fax] IS NULL)) OR NOT (Target.[Phone] = Source.[Phone] OR (Target.[Phone] IS NULL AND Source.[Phone] IS NULL)) OR NOT (Target.[PostalCode] = Source.[PostalCode] OR (Target.[PostalCode] IS NULL AND Source.[PostalCode] IS NULL)) OR NOT (Target.[Region] = Source.[Region] OR (Target.[Region] IS NULL AND Source.[Region] IS NULL))) THEN
  UPDATE SET
        [Address] = Source.[Address],
        [City] = Source.[City],
        [CompanyName] = Source.[CompanyName],
        [ContactName] = Source.[ContactName],
        [ContactTitle] = Source.[ContactTitle],
        [Country] = Source.[Country],
        [CustomerID] = Source.[CustomerID],
        [Fax] = Source.[Fax],
        [Phone] = Source.[Phone],
        [PostalCode] = Source.[PostalCode],
        [Region] = Source.[Region]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Address],
        [City],
        [CompanyName],
        [ContactName],
        [ContactTitle],
        [Country],
        [CustomerID],
        [Fax],
        [Phone],
        [PostalCode],
        [Region]
   ) VALUES (
         Source.[Address],
        Source.[City],
        Source.[CompanyName],
        Source.[ContactName],
        Source.[ContactTitle],
        Source.[Country],
        Source.[CustomerID],
        Source.[Fax],
        Source.[Phone],
        Source.[PostalCode],
        Source.[Region]
   )
 ;
