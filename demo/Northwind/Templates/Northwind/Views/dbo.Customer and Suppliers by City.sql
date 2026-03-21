SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO


CREATE OR ALTER view [dbo].[Customer and Suppliers by City] AS

SELECT City, CompanyName, ContactName, 'Customers' AS Relationship 
FROM Customers
UNION SELECT City, CompanyName, ContactName, 'Suppliers'
FROM Suppliers
--ORDER BY City, CompanyName

GO
