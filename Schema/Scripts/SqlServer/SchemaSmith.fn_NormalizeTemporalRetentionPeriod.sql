-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- #depth-gap: SQL Server's HISTORY_RETENTION_PERIOD DDL grammar accepts both singular and plural unit
-- keywords (DAY|DAYS, WEEK|WEEKS, MONTH|MONTHS, YEAR|YEARS), but sys.tables always reports the unit back
-- singular (history_retention_period_unit_desc = 'YEAR', never 'YEARS'). Extraction and the deploy-side
-- live-state comparison both canonicalize to plural (see GenerateTableJson.sql/GenerateTableXml.sql and
-- MissingIndexesAndConstraintsQuench.sql). Without normalizing the DECLARED side the same way at parse
-- time, an author who writes valid-but-singular "5 YEAR" would never compare equal to the canonical "5
-- YEARS" the live-state read produces -- churning the ALTER on every single deploy. Normalized upstream
-- once here (parse time), the same way the PostgreSQL FK SET DEFAULT fix normalized its declared side, so
-- every downstream consumer (the deploy comparison, the emitted ALTER text) compares/emits like-for-like.
IF OBJECT_ID('SchemaSmith.fn_NormalizeTemporalRetentionPeriod') IS NOT NULL DROP FUNCTION SchemaSmith.fn_NormalizeTemporalRetentionPeriod
GO
CREATE FUNCTION SchemaSmith.fn_NormalizeTemporalRetentionPeriod(@p_Input NVARCHAR(50))
  RETURNS NVARCHAR(50)
AS
BEGIN
  DECLARE @v_Input NVARCHAR(50) = UPPER(RTRIM(LTRIM(ISNULL(@p_Input, ''))))
  IF @v_Input = '' RETURN NULL
  IF @v_Input = 'INFINITE' RETURN 'INFINITE'

  DECLARE @v_SpacePos INT = CHARINDEX(' ', @v_Input)
  -- Malformed input (no unit word, e.g. a bare number) round-trips unchanged -- SQL Server's own DDL
  -- parser rejects it with its own clear error rather than this function guessing at an intended unit.
  IF @v_SpacePos = 0 RETURN @v_Input

  DECLARE @v_Number NVARCHAR(20) = LEFT(@v_Input, @v_SpacePos - 1)
  DECLARE @v_Unit NVARCHAR(20) = SUBSTRING(@v_Input, @v_SpacePos + 1, LEN(@v_Input))

  RETURN @v_Number + ' ' + CASE @v_Unit
    WHEN 'DAY' THEN 'DAYS' WHEN 'DAYS' THEN 'DAYS'
    WHEN 'WEEK' THEN 'WEEKS' WHEN 'WEEKS' THEN 'WEEKS'
    WHEN 'MONTH' THEN 'MONTHS' WHEN 'MONTHS' THEN 'MONTHS'
    WHEN 'YEAR' THEN 'YEARS' WHEN 'YEARS' THEN 'YEARS'
    ELSE @v_Unit -- unrecognized unit word -- left as-is for SQL Server's own DDL parser to reject
  END
END
