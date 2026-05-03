SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   VIEW [dbo].[nicer_but_slower_film_list]
AS

SELECT
    f.[film_id] AS [FID],
    f.[title],
    f.[description],
    c.[name] AS [category],
    f.[rental_rate] AS [price],
    f.[length],
    f.[rating],
    STRING_AGG(
        UPPER(SUBSTRING(a.[first_name], 1, 1)) + LOWER(SUBSTRING(a.[first_name], 2, LEN(a.[first_name])))
        + ' '
        + UPPER(SUBSTRING(a.[last_name], 1, 1)) + LOWER(SUBSTRING(a.[last_name], 2, LEN(a.[last_name]))),
        ', '
    ) AS [actors]
FROM [dbo].[category] c
    LEFT JOIN [dbo].[film_category] fc ON c.[category_id] = fc.[category_id]
    LEFT JOIN [dbo].[film] f ON fc.[film_id] = f.[film_id]
    INNER JOIN [dbo].[film_actor] fa ON f.[film_id] = fa.[film_id]
    INNER JOIN [dbo].[actor] a ON fa.[actor_id] = a.[actor_id]
GROUP BY f.[film_id], f.[title], f.[description], c.[name], f.[rental_rate], f.[length], f.[rating];

GO
