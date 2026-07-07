-- Seed the diag_keys baseline. Two arming conditions for Module 3's two beats:
--   Beat 1 (index): CustomerId 1 and 7 share an email — harmless under the non-unique
--     IX_Customer_Email, but the dirty data the UNIQUE flip trips on.
--   Beat 2 (FK): SalesOrder OrderId 3 references CustomerId 999, which has no Customer
--     row — a resident orphan. Harmless while SalesOrder carries no FK, but the row the
--     FK_SalesOrder_Customer add validates against (and fails on) in beat 2.
-- Run-once (no [ALWAYS]): tracked in CompletedMigrationScripts, so it seeds on the
-- baseline deploy and never re-runs — which is what lets the manual data fixes stick.
IF NOT EXISTS (SELECT 1 FROM dbo.Customer)
INSERT dbo.Customer ([CustomerId],[Email],[FullName]) VALUES
  (1, 'ana.f@shop.test',   'Ana Fielding'),
  (2, 'ben.c@shop.test',   'Ben Cortez'),
  (3, 'carla.d@shop.test', 'Carla Dunn'),
  (7, 'ana.f@shop.test',   'Ana Fielding-Reyes');

IF NOT EXISTS (SELECT 1 FROM dbo.SalesOrder)
INSERT dbo.SalesOrder ([OrderId],[CustomerId],[OrderDate],[Status]) VALUES
  (1, 1,   '2026-01-05T00:00:00', 'Shipped'),
  (2, 2,   '2026-01-06T00:00:00', 'Shipped'),
  (3, 999, '2026-01-07T00:00:00', 'Open');   -- resident orphan: CustomerId 999 has no Customer
