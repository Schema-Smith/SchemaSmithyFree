-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- All-versions-safe comma/list splitter replacing STRING_SPLIT, which requires compatibility level 130+
-- ("Invalid object name 'STRING_SPLIT'" below it). Used by the legacy (XML) ingest/compare procs so they
-- run at compatibility level 100 (SQL Server 2008). Returns [Ordinal] (1-based input position) so callers
-- can rebuild a list in its original order via FOR XML PATH; [value] matches STRING_SPLIT's column name so
-- existing WHERE filters port unchanged. An inline table-valued function (no scalar-UDF row cost); the
-- 10,000-row tally caps list length well above any column/index list.
CREATE OR ALTER FUNCTION SchemaSmith.fn_SplitList(@List NVARCHAR(MAX), @Delimiter NCHAR(1))
RETURNS TABLE
AS RETURN
  WITH E1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
       E4(n) AS (SELECT 1 FROM E1 a CROSS JOIN E1 b CROSS JOIN E1 c CROSS JOIN E1 d),
       Tally(n) AS (SELECT TOP (ISNULL(LEN(@List), 0) + 1) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) FROM E4)
  SELECT [Ordinal] = ROW_NUMBER() OVER (ORDER BY n),
         [value] = SUBSTRING(@List, n, ISNULL(NULLIF(CHARINDEX(@Delimiter, @List, n), 0), LEN(@List) + 1) - n)
    FROM Tally
   WHERE n = 1 OR SUBSTRING(@List, n - 1, 1) = @Delimiter;
