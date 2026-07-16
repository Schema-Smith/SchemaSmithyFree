-- A one-time backfill that only makes sense when upgrading from the legacy schema. Whether it
-- should run depends on what's on the target, and MySQL can't branch in a plain batch — so the
-- decision lives in a short throwaway procedure: if there's no legacy table to migrate from
-- (a fresh install), it raises the sentinel and SchemaQuench records it as an intentional skip,
-- not a failure. Because it's a run-once migration, the skip is recorded as completed and never
-- retried on this database.
DROP PROCEDURE IF EXISTS _BackfillLegacyRegions;
CREATE PROCEDURE _BackfillLegacyRegions()
BEGIN
    IF (SELECT COUNT(*) FROM information_schema.tables
          WHERE table_schema = DATABASE() AND table_name = 'LegacyOrders') = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SCHEMASMITH: SHOULD NOT APPLY';
    END IF;
    -- A real upgrade would copy rows from LegacyOrders into the new schema here.
END;
CALL _BackfillLegacyRegions();
DROP PROCEDURE IF EXISTS _BackfillLegacyRegions;
