SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO
-- Legacy variant: identical result set to the Views-Modern copy, built with
-- FOR XML PATH instead of STRING_AGG, which is SQL Server 2017+.
-- STRING_AGG's WITHIN GROUP (ORDER BY ...) becomes a plain ORDER BY inside the
-- correlated subquery; FOR XML PATH has no WITHIN GROUP form.
CREATE OR ALTER   VIEW [dbo].[actor_info]
AS

WITH actor_category_films AS (
    SELECT
        fa.[actor_id],
        c.[category_id],
        c.[name] AS category_name,
        STUFF((SELECT ', ' + f2.[title]
                 FROM [dbo].[film_actor] fa2
                     INNER JOIN [dbo].[film] f2 ON fa2.[film_id] = f2.[film_id]
                     INNER JOIN [dbo].[film_category] fc2 ON f2.[film_id] = fc2.[film_id]
                 WHERE fa2.[actor_id] = fa.[actor_id]
                   AND fc2.[category_id] = c.[category_id]
                 ORDER BY f2.[title]
                 FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS film_titles
    FROM [dbo].[film_actor] fa
        INNER JOIN [dbo].[film] f ON fa.[film_id] = f.[film_id]
        INNER JOIN [dbo].[film_category] fc ON f.[film_id] = fc.[film_id]
        INNER JOIN [dbo].[category] c ON fc.[category_id] = c.[category_id]
    GROUP BY fa.[actor_id], c.[category_id], c.[name]
)
SELECT
    a.[actor_id],
    a.[first_name],
    a.[last_name],
    STUFF((SELECT '; ' + CAST(acf2.category_name + ': ' + acf2.film_titles AS NVARCHAR(MAX))
             FROM actor_category_films acf2
             WHERE acf2.[actor_id] = a.[actor_id]
             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS [film_info]
FROM [dbo].[actor] a
    LEFT JOIN actor_category_films acf ON a.[actor_id] = acf.[actor_id]
GROUP BY a.[actor_id], a.[first_name], a.[last_name];

GO
