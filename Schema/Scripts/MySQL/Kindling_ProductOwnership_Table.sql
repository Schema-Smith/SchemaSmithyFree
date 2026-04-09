-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- ProductOwnership: Tracks which database objects are owned by which product/template
-- Used by SchemaQuench for validation and cleanup
-- This table is created in each target database with SchemaSmith_ prefix

CREATE TABLE IF NOT EXISTS `SchemaSmith_ProductOwnership` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `ObjectType` VARCHAR(50) NOT NULL,       -- TABLE, INDEX, CONSTRAINT, VIEW, PROCEDURE, FUNCTION, TRIGGER
    `ObjectSchema` VARCHAR(64) NOT NULL,     -- Database name
    `ObjectName` VARCHAR(64) NOT NULL,       -- Object name (without backticks)
    `ProductName` VARCHAR(100) NOT NULL,
    `TemplateName` VARCHAR(100) NOT NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY `uk_object` (`ObjectType`, `ObjectSchema`, `ObjectName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
