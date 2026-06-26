-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- A component ShouldApplyExpression is embedded as a bare predicate inside NOT (<expr>). Accept the
-- folder-gate form too -- a projection-only SELECT -- by stripping a leading SELECT keyword so the
-- remainder is a usable predicate. Either form then works on any component gate (#282). The match
-- requires SELECT followed by whitespace, so an identifier like "selected" is not mistaken for the
-- keyword; a non-SELECT expression is returned unchanged.
CREATE OR REPLACE FUNCTION "SchemaSmith"."StripLeadingSelect"(p_text TEXT) RETURNS TEXT
    LANGUAGE sql IMMUTABLE
AS $$
  SELECT CASE
           WHEN p_text IS NULL THEN p_text
           WHEN ltrim(p_text) ~* '^select\s' THEN regexp_replace(ltrim(p_text), '^select\s+', '', 'i')
           ELSE p_text
         END;
$$;
