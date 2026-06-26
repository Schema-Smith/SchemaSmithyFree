-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- A component ShouldApplyExpression is embedded as a bare predicate inside NOT (<expr>). Accept the
-- folder-gate form too -- a projection-only SELECT -- by stripping a leading SELECT keyword so the
-- remainder is a usable predicate. Either form then works on any component gate (#282). A non-SELECT
-- expression is returned unchanged; the match requires SELECT followed by whitespace so identifiers
-- like SELECTED are not mistaken for the keyword.
CREATE OR ALTER FUNCTION SchemaSmith.fn_StripLeadingSelect(@p_Input NVARCHAR(MAX))
  RETURNS NVARCHAR(MAX)
AS
BEGIN
  DECLARE @v_Trimmed NVARCHAR(MAX) = LTRIM(ISNULL(@p_Input, ''))
  IF LEN(@v_Trimmed) >= 7
     AND UPPER(LEFT(@v_Trimmed, 6)) = 'SELECT'
     AND SUBSTRING(@v_Trimmed, 7, 1) LIKE '[' + CHAR(32) + CHAR(9) + CHAR(10) + CHAR(13) + ']'
    RETURN LTRIM(STUFF(@v_Trimmed, 1, 6, ''))
  RETURN @p_Input
END
