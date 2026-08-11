SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   VIEW [dbo].[actor_info]
AS

WITH actor_category_films AS (
    SELECT
        fa.[actor_id],
        c.[category_id],
        c.[name] AS category_name,
        STRING_AGG(f.[title], ', ') WITHIN GROUP (ORDER BY f.[title]) AS film_titles
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
    STRING_AGG(CAST(acf.category_name + ': ' + acf.film_titles AS NVARCHAR(MAX)), '; ') AS [film_info]
FROM [dbo].[actor] a
    LEFT JOIN actor_category_films acf ON a.[actor_id] = acf.[actor_id]
GROUP BY a.[actor_id], a.[first_name], a.[last_name];

GO
