-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Normalize a SQL expression string for case- and whitespace-insensitive
-- comparison. SQL Server stores sys.computed_columns.[definition] in a
-- canonical form (lowercase function/keyword names, no whitespace around
-- commas/parens, full paren wrapping). JSON-declared expressions are
-- round-tripped verbatim and may be authored in any case + spacing style.
-- Comparing without normalizing both sides falsely flags drift on every
-- re-quench, kicking off a destructive drop+re-add cycle.
--
-- Normalization steps:
--   (1) Strip outer paren wrapping (delegates to fn_StripParenWrapping)
--   (2) Lowercase everything (matches the dialect SQL Server stores)
--   (3) Strip all whitespace (space, tab, CR, LF)
--
-- Bracketed identifiers like [RetentionDays] survive intact through both
-- sides (SQL Server preserves case inside brackets in stored expressions),
-- so step (2) is safe. Step (3) is safe even for identifiers with spaces
-- like [Order Details] because both sides normalize identically.

CREATE OR ALTER FUNCTION SchemaSmith.fn_NormalizeExpression(@p_Input NVARCHAR(MAX))
  RETURNS NVARCHAR(MAX)
AS
BEGIN
  RETURN LOWER(REPLACE(REPLACE(REPLACE(REPLACE(
           SchemaSmith.fn_StripParenWrapping(@p_Input),
           CHAR(9),  ''),  -- tab
           CHAR(10), ''),  -- LF
           CHAR(13), ''),  -- CR
           ' ',      ''))  -- space
END
