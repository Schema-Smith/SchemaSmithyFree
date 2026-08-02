-- MODERN variant — deployed only where database compatibility level >= 160.
--
-- GENERATE_SERIES is a SQL Server 2022 table-valued function AND it is gated by
-- database compatibility level: on a 2022 binary it still fails with
-- "Msg 208 ... Invalid object name 'GENERATE_SERIES'" if the database sits at 130.
-- That is why the folder gate in Template.json tests compatibility_level and NOT
-- SERVERPROPERTY('ProductMajorVersion').
--
-- Paired with Programmability/Legacy/dbo.vReadingCalendar.sql, which builds the same
-- view with a recursive CTE. Exactly one of the two folders applies per database.

CREATE OR ALTER VIEW dbo.vReadingCalendar
AS
SELECT
    s.value                                                        AS DayOffset,
    DATEADD(DAY, s.value, CAST('2026-01-01' AS DATE))              AS CalendarDate,
    (
        SELECT COUNT_BIG(*)
        FROM dbo.Reading r
        WHERE CAST(r.TakenAt AS DATE) = DATEADD(DAY, s.value, CAST('2026-01-01' AS DATE))
    )                                                              AS ReadingCount
FROM GENERATE_SERIES(0, 29) AS s;
