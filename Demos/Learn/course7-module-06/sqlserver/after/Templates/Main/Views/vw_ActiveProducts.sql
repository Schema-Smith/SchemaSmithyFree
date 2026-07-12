CREATE OR ALTER VIEW [dbo].[vw_ActiveProducts]
AS
SELECT [ProductId], [Name], [Sku], [UnitPrice]
FROM [dbo].[Product];
