-- LEGACY variant — folder-gated to deploy ONLY where the database's
-- compatibility level is < 160 (see Template.json → Programmability/Legacy).
--
-- Same view name, same columns, same rows as the Modern variant. Builds the day
-- series with a recursive CTE instead of GENERATE_SERIES, because that function
-- does not resolve below compatibility level 160.
--
-- 30 rows stays under the default MAXRECURSION of 100, so the view needs no
-- OPTION (MAXRECURSION) hint — which matters, because that hint is only legal on
-- the outer query and cannot live inside a view definition.

CREATE OR ALTER VIEW dbo.vReadingCalendar
AS
WITH DaySeries AS
(
    SELECT 0 AS DayOffset
    UNION ALL
    SELECT DayOffset + 1
    FROM DaySeries
    WHERE DayOffset < 29
)
SELECT
    d.DayOffset                                                    AS DayOffset,
    DATEADD(DAY, d.DayOffset, CAST('2026-01-01' AS DATE))          AS CalendarDate,
    (
        SELECT COUNT_BIG(*)
        FROM dbo.Reading r
        WHERE CAST(r.TakenAt AS DATE) = DATEADD(DAY, d.DayOffset, CAST('2026-01-01' AS DATE))
    )                                                              AS ReadingCount
FROM DaySeries d;
