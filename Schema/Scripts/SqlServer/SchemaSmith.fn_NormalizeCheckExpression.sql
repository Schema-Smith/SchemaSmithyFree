-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

IF OBJECT_ID('SchemaSmith.fn_NormalizeCheckExpression') IS NOT NULL
  DROP FUNCTION SchemaSmith.fn_NormalizeCheckExpression
GO

-- Folds a check-constraint expression to the form SQL Server itself stores, so a declared expression can be
-- compared with the catalog's canonical rendering of the same thing.
--
-- SQL Server rewrites what you write: [RetentionDays] <= 365 is stored as ([RetentionDays]<=(365)) -- outer
-- parens, parens around the literal, spaces around operators removed. None of that survived a text
-- comparison, so a constraint was dropped and re-created on EVERY deploy. That break exits 0 and logs
-- success, so nothing surfaces it; the user just sees the constraint churn forever.
--
-- Deliberately narrow. It undoes only the three things the engine adds, and never removes a parenthesis that
-- groups an expression -- over-normalizing would make two genuinely different constraints compare equal and
-- suppress a re-create that was needed, which is a worse failure than the churn it fixes.
CREATE FUNCTION SchemaSmith.fn_NormalizeCheckExpression(@p_Expression NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
  IF @p_Expression IS NULL RETURN NULL

  DECLARE @v_Result NVARCHAR(MAX) = SchemaSmith.fn_StripParenWrapping(LTRIM(RTRIM(@p_Expression)))

  -- Spaces adjacent to an operator or punctuation only. A space BETWEEN word characters is left alone, so
  -- `IS NOT NULL` keeps its shape and a literal like 'two words' is not silently glued together.
  DECLARE @v_Previous NVARCHAR(MAX) = N''
  WHILE @v_Result <> @v_Previous
  BEGIN
    SET @v_Previous = @v_Result
    SET @v_Result = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@v_Result,
                      ' <', '<'), '< ', '<'), ' >', '>'), '> ', '>'),
                      ' =', '='), '= ', '='), ' (', '('), '( ', '('),
                      ' )', ')'), ') ', ')'), ' ,', ','), ', ', ','),
                      ' +', '+'), '+ ', '+')
  END

  -- Parens the engine puts around a bare numeric literal: (365) -> 365. Bounded by the loop's own progress,
  -- so a malformed expression cannot spin here.
  SET @v_Previous = N''
  WHILE @v_Result <> @v_Previous
  BEGIN
    SET @v_Previous = @v_Result
    DECLARE @v_Pos INT = PATINDEX('%(%[0-9]%)%', @v_Result)
    IF @v_Pos = 0 BREAK
    DECLARE @v_Close INT = CHARINDEX(')', @v_Result, @v_Pos)
    IF @v_Close = 0 BREAK
    DECLARE @v_Inner NVARCHAR(MAX) = SUBSTRING(@v_Result, @v_Pos + 1, @v_Close - @v_Pos - 1)
    IF @v_Inner NOT LIKE '%[^0-9.-]%' AND LEN(@v_Inner) > 0
      SET @v_Result = STUFF(@v_Result, @v_Pos, @v_Close - @v_Pos + 1, @v_Inner)
    ELSE
      BREAK
  END

  RETURN @v_Result
END
GO
