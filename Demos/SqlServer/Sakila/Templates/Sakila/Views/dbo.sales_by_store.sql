SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   VIEW [dbo].[sales_by_store]
AS

SELECT
    ci.[city] + ',' + co.[country] AS [store],
    m.[first_name] + ' ' + m.[last_name] AS [manager],
    SUM(p.[amount]) AS [total_sales]
FROM [dbo].[payment] p
    INNER JOIN [dbo].[rental] r ON p.[rental_id] = r.[rental_id]
    INNER JOIN [dbo].[inventory] i ON r.[inventory_id] = i.[inventory_id]
    INNER JOIN [dbo].[store] s ON i.[store_id] = s.[store_id]
    INNER JOIN [dbo].[address] a ON s.[address_id] = a.[address_id]
    INNER JOIN [dbo].[city] ci ON a.[city_id] = ci.[city_id]
    INNER JOIN [dbo].[country] co ON ci.[country_id] = co.[country_id]
    INNER JOIN [dbo].[staff] m ON s.[manager_staff_id] = m.[staff_id]
GROUP BY co.[country], ci.[city], s.[store_id], m.[first_name], m.[last_name];

GO
