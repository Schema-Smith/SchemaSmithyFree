-- A one-time backfill that only makes sense when upgrading from the legacy schema. Whether it
-- should run can't be written as a one-line ShouldApplyExpression — it depends on what's actually
-- on the target. So the script decides at runtime: if there's no legacy table to migrate from
-- (a fresh install), it raises the sentinel and SchemaQuench records it as an intentional skip,
-- not a failure. Because it's a run-once migration, the skip is recorded as completed and the
-- script is never retried on this database.
IF OBJECT_ID('dbo.LegacyOrders', 'U') IS NULL
BEGIN
    RAISERROR('SCHEMASMITH: SHOULD NOT APPLY', 16, 1);
    RETURN;
END;

-- A real upgrade would copy rows from dbo.LegacyOrders into the new schema here. On a fresh
-- install LegacyOrders doesn't exist, so the sentinel above skips this script before this runs.
INSERT INTO [dbo].[Orders] ([OrderId], [Region])
SELECT [Id], [Region] FROM [dbo].[LegacyOrders];
