-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- StripLeadingSelect: a component ShouldApplyExpression is embedded as a bare predicate inside
-- NOT (<expr>). Accept the folder-gate form too -- a projection-only SELECT -- by stripping a
-- leading SELECT keyword so the remainder is a usable predicate. Either form then works on any
-- component gate (#282). The match requires SELECT followed by whitespace, so an identifier like
-- "selected" is not mistaken for the keyword; a non-SELECT expression is returned unchanged.

DROP FUNCTION IF EXISTS `SchemaSmith_StripLeadingSelect`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_StripLeadingSelect`(p_text VARCHAR(4000))
RETURNS VARCHAR(4000) CHARSET utf8mb4 COLLATE utf8mb4_unicode_ci
DETERMINISTIC
NO SQL
BEGIN
    IF p_text IS NULL THEN
        RETURN p_text;
    END IF;
    IF TRIM(p_text) REGEXP '^[Ss][Ee][Ll][Ee][Cc][Tt][[:space:]]' THEN
        RETURN REGEXP_REPLACE(TRIM(p_text), '^[Ss][Ee][Ll][Ee][Cc][Tt][[:space:]]+', '');
    END IF;
    RETURN p_text;
END //

DELIMITER ;
