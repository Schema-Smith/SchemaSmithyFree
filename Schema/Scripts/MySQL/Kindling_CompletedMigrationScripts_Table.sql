-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Track completed migration scripts to prevent re-execution
-- Note: Key prefix lengths are limited due to InnoDB max key length of 3072 bytes with utf8mb4
-- This table is created in each target database with SchemaSmith_ prefix

CREATE TABLE IF NOT EXISTS `SchemaSmith_CompletedMigrationScripts` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `ProductName` VARCHAR(100) NOT NULL,
    `QuenchSlot` VARCHAR(50) NOT NULL,
    `ScriptPath` VARCHAR(500) NOT NULL,
    `template_name` VARCHAR(255) NOT NULL DEFAULT '',
    `schema_name` VARCHAR(255) NOT NULL DEFAULT '',
    `CompletedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `uk_script` (`ProductName`, `QuenchSlot`, `ScriptPath`(200), `template_name`(50), `schema_name`(50))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- The secondary index ix_completedmigrationscripts_slot_scope on
-- (ProductName, QuenchSlot, template_name, schema_name) is created in
-- Kindling_AlterCompletedMigrationScripts.sql AFTER the legacy-table upgrade adds
-- template_name + schema_name columns. Creating the index here would fail against pre-slice-2
-- tables that still lack those columns; deferring it puts the DDL behind the same idempotent
-- column-presence guard the legacy upgrade already uses.
