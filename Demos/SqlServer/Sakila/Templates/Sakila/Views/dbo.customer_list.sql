SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   VIEW [dbo].[customer_list]
AS

SELECT
    cu.[customer_id] AS [ID],
    cu.[first_name] + ' ' + cu.[last_name] AS [name],
    a.[address],
    a.[postal_code] AS [zip code],
    a.[phone],
    ci.[city],
    co.[country],
    CASE WHEN cu.[active] = 1 THEN 'active' ELSE '' END AS [notes],
    cu.[store_id] AS [SID]
FROM [dbo].[customer] cu
    INNER JOIN [dbo].[address] a ON cu.[address_id] = a.[address_id]
    INNER JOIN [dbo].[city] ci ON a.[city_id] = ci.[city_id]
    INNER JOIN [dbo].[country] co ON ci.[country_id] = co.[country_id];

GO
