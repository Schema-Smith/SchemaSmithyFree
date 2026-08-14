SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO
-- Legacy variant: identical result set to the Views-Modern copy, built with
-- FOR XML PATH instead of STRING_AGG, which is SQL Server 2017+.
-- The modern query's INNER JOINs to film_actor/actor act as a filter as well as
-- a source, so the correlated subquery is paired with an EXISTS to keep the
-- same rows.
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
    STUFF((SELECT ', '
                  + UPPER(SUBSTRING(a.[first_name], 1, 1)) + LOWER(SUBSTRING(a.[first_name], 2, LEN(a.[first_name])))
                  + ' '
                  + UPPER(SUBSTRING(a.[last_name], 1, 1)) + LOWER(SUBSTRING(a.[last_name], 2, LEN(a.[last_name])))
             FROM [dbo].[film_actor] fa
                 INNER JOIN [dbo].[actor] a ON fa.[actor_id] = a.[actor_id]
             WHERE fa.[film_id] = f.[film_id]
             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS [actors]
FROM [dbo].[category] c
    LEFT JOIN [dbo].[film_category] fc ON c.[category_id] = fc.[category_id]
    LEFT JOIN [dbo].[film] f ON fc.[film_id] = f.[film_id]
WHERE EXISTS (SELECT 1
                FROM [dbo].[film_actor] fa2
                    INNER JOIN [dbo].[actor] a2 ON fa2.[actor_id] = a2.[actor_id]
                WHERE fa2.[film_id] = f.[film_id])
GROUP BY f.[film_id], f.[title], f.[description], c.[name], f.[rental_rate], f.[length], f.[rating];

GO
