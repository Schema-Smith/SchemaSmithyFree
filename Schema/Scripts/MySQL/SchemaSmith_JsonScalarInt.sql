-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DROP FUNCTION IF EXISTS `SchemaSmith_JsonScalarInt`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_JsonScalarInt`(p_val JSON)
RETURNS BIGINT
DETERMINISTIC
NO SQL
BEGIN
  -- Convert a JSON scalar (as returned by JSON_EXTRACT) into a clean SQL integer for a numeric/boolean
  -- column: JSON boolean -> 1/0, JSON number or quoted number -> its value, JSON null / SQL NULL -> NULL.
  -- Used in place of a bare JSON_EXTRACT for numeric/boolean fields in the version-agnostic parse so that
  -- (a) an explicit JSON null does not reach a numeric CAST (which errors "Invalid JSON value for CAST to
  -- INTEGER"), and (b) a JSON boolean does not stringify to 'true'/'false' when it flows through COALESCE
  -- or into a typed column. Pure computation on 5.7+ / 10.2+ built-ins (no JSON_TABLE / JSON_VALUE).
  IF p_val IS NULL OR JSON_TYPE(p_val) = 'NULL' THEN
    RETURN NULL;
  END IF;
  IF JSON_TYPE(p_val) = 'BOOLEAN' THEN
    RETURN IF(p_val = CAST('true' AS JSON), 1, 0);
  END IF;
  RETURN CAST(JSON_UNQUOTE(p_val) AS SIGNED);
END //

DELIMITER ;
