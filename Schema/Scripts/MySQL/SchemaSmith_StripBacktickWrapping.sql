-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- StripBacktickWrapping: Removes surrounding backticks from an identifier
-- Also unescapes embedded double-backticks

DROP FUNCTION IF EXISTS `SchemaSmith_StripBacktickWrapping`;

DELIMITER //

CREATE FUNCTION `SchemaSmith_StripBacktickWrapping`(identifier VARCHAR(260))
RETURNS VARCHAR(255) CHARSET utf8mb4 COLLATE utf8mb4_unicode_ci
DETERMINISTIC
NO SQL
BEGIN
    DECLARE result VARCHAR(255);

    -- Remove surrounding backticks
    SET result = TRIM(BOTH '`' FROM identifier);

    -- Unescape embedded double-backticks
    SET result = REPLACE(result, '``', '`');

    RETURN result;
END //

DELIMITER ;
