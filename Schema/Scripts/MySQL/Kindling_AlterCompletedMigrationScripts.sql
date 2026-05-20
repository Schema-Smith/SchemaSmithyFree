-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Idempotent migration: adds template_name and schema_name columns to legacy
-- SchemaSmith_CompletedMigrationScripts tables and extends the uk_script unique
-- key to include them. Safe to run repeatedly. Required for the schema-templates
-- feature's per-(template, schema) tracking key, even though MySQL itself is
-- excluded from schema-template fan-out.
--
-- MySQL 8.0.29+ has ALTER TABLE ADD COLUMN IF NOT EXISTS, but the test container
-- (mysql:8.0) ships earlier patches in some environments. The information_schema
-- guard is the portable alternative across 8.0.x patch levels.

DROP PROCEDURE IF EXISTS `schemasmith_migrate_completed_scripts`;

DELIMITER //
CREATE PROCEDURE `schemasmith_migrate_completed_scripts`()
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = DATABASE()
                 AND table_name = 'SchemaSmith_CompletedMigrationScripts') THEN

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = DATABASE()
                         AND table_name = 'SchemaSmith_CompletedMigrationScripts'
                         AND column_name = 'template_name') THEN
            ALTER TABLE `SchemaSmith_CompletedMigrationScripts`
                ADD COLUMN `template_name` VARCHAR(255) NOT NULL DEFAULT '';
        END IF;

        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = DATABASE()
                         AND table_name = 'SchemaSmith_CompletedMigrationScripts'
                         AND column_name = 'schema_name') THEN
            ALTER TABLE `SchemaSmith_CompletedMigrationScripts`
                ADD COLUMN `schema_name` VARCHAR(255) NOT NULL DEFAULT '';
        END IF;

        IF (SELECT COUNT(*) FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = 'SchemaSmith_CompletedMigrationScripts'
              AND index_name = 'uk_script') = 3 THEN
            ALTER TABLE `SchemaSmith_CompletedMigrationScripts` DROP INDEX `uk_script`;
            ALTER TABLE `SchemaSmith_CompletedMigrationScripts`
                ADD UNIQUE KEY `uk_script`
                (`ProductName`, `QuenchSlot`, `ScriptPath`(200), `template_name`(50), `schema_name`(50));
        END IF;

        -- Secondary index for GetCompletedEntriesBySlot lookups. The PK (`Id`) and uk_script
        -- both miss the (ProductName, QuenchSlot, template_name, schema_name) lookup pattern
        -- that GetCompletedEntriesBySlot filters by. Adding it here (rather than in the
        -- table-creation script) means the index DDL runs AFTER the legacy column-add migration
        -- above, so it never fires against a pre-slice-2 table shape lacking those columns.
        IF NOT EXISTS (SELECT 1 FROM information_schema.statistics
                       WHERE table_schema = DATABASE()
                         AND table_name = 'SchemaSmith_CompletedMigrationScripts'
                         AND index_name = 'ix_completedmigrationscripts_slot_scope') THEN
            ALTER TABLE `SchemaSmith_CompletedMigrationScripts`
                ADD INDEX `ix_completedmigrationscripts_slot_scope`
                (`ProductName`, `QuenchSlot`, `template_name`(50), `schema_name`(50));
        END IF;
    END IF;
END //
DELIMITER ;

CALL `schemasmith_migrate_completed_scripts`();
DROP PROCEDURE `schemasmith_migrate_completed_scripts`;
