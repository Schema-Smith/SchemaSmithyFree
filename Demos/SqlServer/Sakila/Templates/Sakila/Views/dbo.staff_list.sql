SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   VIEW [dbo].[staff_list]
AS

SELECT
    s.[staff_id] AS [ID],
    s.[first_name] + ' ' + s.[last_name] AS [name],
    a.[address],
    a.[postal_code] AS [zip code],
    a.[phone],
    ci.[city],
    co.[country],
    s.[store_id] AS [SID]
FROM [dbo].[staff] s
    INNER JOIN [dbo].[address] a ON s.[address_id] = a.[address_id]
    INNER JOIN [dbo].[city] ci ON a.[city_id] = ci.[city_id]
    INNER JOIN [dbo].[country] co ON ci.[country_id] = co.[country_id];

GO
