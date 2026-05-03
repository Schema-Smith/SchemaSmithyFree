-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- QuoteIdentifier: Safely wraps an identifier with backticks
-- Handles already-quoted identifiers and escapes embedded backticks

DROP FUNCTION IF EXISTS `SchemaSmith_QuoteIdentifier`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_QuoteIdentifier`(identifier VARCHAR(255))
RETURNS VARCHAR(260) CHARSET utf8mb4 COLLATE utf8mb4_unicode_ci
DETERMINISTIC
NO SQL
BEGIN
    DECLARE unquoted VARCHAR(255);

    -- Remove existing backticks if present
    SET unquoted = TRIM(BOTH '`' FROM identifier);

    -- Escape any embedded backticks and wrap
    RETURN CONCAT('`', REPLACE(unquoted, '`', '``'), '`');
END //

DELIMITER ;
