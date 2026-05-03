SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   VIEW [dbo].[sales_by_film_category]
AS

SELECT
    c.[name] AS [category],
    SUM(p.[amount]) AS [total_sales]
FROM [dbo].[payment] p
    INNER JOIN [dbo].[rental] r ON p.[rental_id] = r.[rental_id]
    INNER JOIN [dbo].[inventory] i ON r.[inventory_id] = i.[inventory_id]
    INNER JOIN [dbo].[film] f ON i.[film_id] = f.[film_id]
    INNER JOIN [dbo].[film_category] fc ON f.[film_id] = fc.[film_id]
    INNER JOIN [dbo].[category] c ON fc.[category_id] = c.[category_id]
GROUP BY c.[name];

GO
