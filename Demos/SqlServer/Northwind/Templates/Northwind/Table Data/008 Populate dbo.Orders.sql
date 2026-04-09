
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Orders.tabledata}}';


SET IDENTITY_INSERT [dbo].[Orders] ON;
MERGE INTO [dbo].[Orders] AS Target
USING (
  SELECT [CustomerID],[EmployeeID],[Freight],[OrderDate],[OrderID],[RequiredDate],[ShipAddress],[ShipCity],[ShipCountry],[ShipName],[ShippedDate],[ShipPostalCode],[ShipRegion],[ShipVia]
    FROM OPENJSON(@v_json)
    WITH (
           [CustomerID] NCHAR(5),
           [EmployeeID] INT,
           [Freight] MONEY,
           [OrderDate] DATETIME,
           [OrderID] INT,
           [RequiredDate] DATETIME,
           [ShipAddress] NVARCHAR(60),
           [ShipCity] NVARCHAR(15),
           [ShipCountry] NVARCHAR(15),
           [ShipName] NVARCHAR(40),
           [ShippedDate] DATETIME,
           [ShipPostalCode] NVARCHAR(10),
           [ShipRegion] NVARCHAR(15),
           [ShipVia] INT
    )
) AS Source
ON Source.[OrderID] = Target.[OrderID]

WHEN MATCHED AND (NOT (Target.[CustomerID] = Source.[CustomerID] OR (Target.[CustomerID] IS NULL AND Source.[CustomerID] IS NULL)) OR NOT (Target.[EmployeeID] = Source.[EmployeeID] OR (Target.[EmployeeID] IS NULL AND Source.[EmployeeID] IS NULL)) OR NOT (Target.[Freight] = Source.[Freight] OR (Target.[Freight] IS NULL AND Source.[Freight] IS NULL)) OR NOT (Target.[OrderDate] = Source.[OrderDate] OR (Target.[OrderDate] IS NULL AND Source.[OrderDate] IS NULL)) OR NOT (Target.[RequiredDate] = Source.[RequiredDate] OR (Target.[RequiredDate] IS NULL AND Source.[RequiredDate] IS NULL)) OR NOT (Target.[ShipAddress] = Source.[ShipAddress] OR (Target.[ShipAddress] IS NULL AND Source.[ShipAddress] IS NULL)) OR NOT (Target.[ShipCity] = Source.[ShipCity] OR (Target.[ShipCity] IS NULL AND Source.[ShipCity] IS NULL)) OR NOT (Target.[ShipCountry] = Source.[ShipCountry] OR (Target.[ShipCountry] IS NULL AND Source.[ShipCountry] IS NULL)) OR NOT (Target.[ShipName] = Source.[ShipName] OR (Target.[ShipName] IS NULL AND Source.[ShipName] IS NULL)) OR NOT (Target.[ShippedDate] = Source.[ShippedDate] OR (Target.[ShippedDate] IS NULL AND Source.[ShippedDate] IS NULL)) OR NOT (Target.[ShipPostalCode] = Source.[ShipPostalCode] OR (Target.[ShipPostalCode] IS NULL AND Source.[ShipPostalCode] IS NULL)) OR NOT (Target.[ShipRegion] = Source.[ShipRegion] OR (Target.[ShipRegion] IS NULL AND Source.[ShipRegion] IS NULL)) OR NOT (Target.[ShipVia] = Source.[ShipVia] OR (Target.[ShipVia] IS NULL AND Source.[ShipVia] IS NULL))) THEN
  UPDATE SET
        [CustomerID] = Source.[CustomerID],
        [EmployeeID] = Source.[EmployeeID],
        [Freight] = Source.[Freight],
        [OrderDate] = Source.[OrderDate],
        [RequiredDate] = Source.[RequiredDate],
        [ShipAddress] = Source.[ShipAddress],
        [ShipCity] = Source.[ShipCity],
        [ShipCountry] = Source.[ShipCountry],
        [ShipName] = Source.[ShipName],
        [ShippedDate] = Source.[ShippedDate],
        [ShipPostalCode] = Source.[ShipPostalCode],
        [ShipRegion] = Source.[ShipRegion],
        [ShipVia] = Source.[ShipVia]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CustomerID],
        [EmployeeID],
        [Freight],
        [OrderDate],
        [OrderID],
        [RequiredDate],
        [ShipAddress],
        [ShipCity],
        [ShipCountry],
        [ShipName],
        [ShippedDate],
        [ShipPostalCode],
        [ShipRegion],
        [ShipVia]
   ) VALUES (
         Source.[CustomerID],
        Source.[EmployeeID],
        Source.[Freight],
        Source.[OrderDate],
        Source.[OrderID],
        Source.[RequiredDate],
        Source.[ShipAddress],
        Source.[ShipCity],
        Source.[ShipCountry],
        Source.[ShipName],
        Source.[ShippedDate],
        Source.[ShipPostalCode],
        Source.[ShipRegion],
        Source.[ShipVia]
   )
 ;
SET IDENTITY_INSERT [dbo].[Orders] OFF;
