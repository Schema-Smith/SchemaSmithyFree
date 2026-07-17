-- Seed the diag_keys baseline. Two arming conditions for Module 3's two beats:
--   Beat 1 (index): CustomerId 1 and 7 share an email — harmless under the non-unique
--     IX_Customer_Email, but the dirty data the UNIQUE flip trips on.
--   Beat 2 (FK): SalesOrder OrderId 3 references CustomerId 999, which has no Customer
--     row — a resident orphan. Harmless while SalesOrder carries no FK, but the row the
--     FK_SalesOrder_Customer add validates against (and fails on) in beat 2.
-- Run-once (no [ALWAYS]): tracked in CompletedMigrationScripts, so it seeds on the
-- baseline deploy and never re-runs — which is what lets the manual data fixes stick.
INSERT INTO `Customer` (`CustomerId`, `Email`, `FullName`)
SELECT * FROM (SELECT 1 AS c, 'ana.f@shop.test' AS e, 'Ana Fielding' AS f
  UNION ALL SELECT 2, 'ben.c@shop.test',   'Ben Cortez'
  UNION ALL SELECT 3, 'carla.d@shop.test', 'Carla Dunn'
  UNION ALL SELECT 7, 'ana.f@shop.test',   'Ana Fielding-Reyes') AS v
WHERE NOT EXISTS (SELECT 1 FROM `Customer`);

INSERT INTO `SalesOrder` (`OrderId`, `CustomerId`, `OrderDate`, `Status`)
SELECT * FROM (SELECT 1 AS o, 1 AS c, '2026-01-05 00:00:00' AS d, 'Shipped' AS s
  UNION ALL SELECT 2, 2,   '2026-01-06 00:00:00', 'Shipped'
  UNION ALL SELECT 3, 999, '2026-01-07 00:00:00', 'Open') AS v
WHERE NOT EXISTS (SELECT 1 FROM `SalesOrder`);
