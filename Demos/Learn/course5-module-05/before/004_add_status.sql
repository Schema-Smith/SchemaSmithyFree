-- 004: the late addition. Status got bolted onto SalesOrder after the fact —
-- the kind of ALTER that a declarative tool would have just folded into the
-- table definition. Guarded against double-apply, because by now everyone knew
-- to guard. The end state matches what's live in shop_from_scripts.
--
-- The add-then-drop-constraint two-step is a classic hand-rolled idiom: the
-- DEFAULT 'New' is only there to backfill the existing rows (you can't add a
-- NOT NULL column to a populated table without one), then it's dropped so the
-- default doesn't linger on future inserts. It's exactly the kind of multi-step
-- workaround a declarative model never makes you write.
IF NOT EXISTS (
  SELECT 1 FROM sys.columns
  WHERE object_id = OBJECT_ID('dbo.SalesOrder') AND name = 'Status'
)
BEGIN
  ALTER TABLE dbo.SalesOrder ADD Status VARCHAR(20) NOT NULL CONSTRAINT DF_SalesOrder_Status DEFAULT 'New';
  ALTER TABLE dbo.SalesOrder DROP CONSTRAINT DF_SalesOrder_Status;
END;

INSERT INTO dbo.schema_version (version, description) VALUES (4, '004_add_status.sql');
