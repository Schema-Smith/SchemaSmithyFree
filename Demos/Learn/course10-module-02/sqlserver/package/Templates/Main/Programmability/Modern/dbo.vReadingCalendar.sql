-- MODERN variant — folder-gated to deploy ONLY where the database's
-- compatibility level is >= 160 (see Template.json → Programmability/Modern).
--
-- GENERATE_SERIES is a SQL Server 2022 table-valued function that is gated by
-- database compatibility level, NOT the server binary: on a 2022 instance it
-- still fails with "Msg 208 ... Invalid object name 'GENERATE_SERIES'" if the
-- database sits at compat 130. That is the footgun this module turns on — and
-- exactly why the gate tests {{CompatibilityLevel}} and not the server version.
--
-- Paired with Programmability/Legacy/dbo.vReadingCalendar.sql, which builds the
-- same view with a recursive CTE. Exactly one of the two folders applies per DB.

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
