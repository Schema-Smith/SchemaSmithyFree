-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_JsonScalarStr`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_JsonScalarStr`(p_val JSON)
RETURNS LONGTEXT
DETERMINISTIC
NO SQL
BEGIN
  -- Convert a JSON scalar (as returned by JSON_EXTRACT) into a SQL string for a text field, mapping an
  -- explicit JSON null to SQL NULL. A bare JSON_UNQUOTE of a JSON null yields the literal string 'null'
  -- (not SQL NULL, which JSON_TABLE produced), which would survive NULLIF(TRIM(...),'') and be treated as
  -- a real value (e.g. a bogus ShouldApplyExpression). Companion to SchemaSmith_JsonScalarInt for the
  -- version-agnostic parse. Pure computation on 5.7+ / 10.2+ built-ins (no JSON_TABLE / JSON_VALUE).
  IF p_val IS NULL OR JSON_TYPE(p_val) = 'NULL' THEN
    RETURN NULL;
  END IF;
  RETURN JSON_UNQUOTE(p_val);
END //

DELIMITER ;
