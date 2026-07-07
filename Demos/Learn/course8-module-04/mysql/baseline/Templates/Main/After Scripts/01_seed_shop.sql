-- Baseline seed for the script-slot + data-delivery sandbox. Three valid customers with
-- unique emails; SalesOrder is left empty (beat 2 delivers into it). Run-once: tracked in
-- CompletedMigrationScripts, seeds on the baseline deploy, never re-runs.
INSERT INTO `Customer` (`CustomerId`, `Email`, `FullName`)
SELECT * FROM (SELECT 1 AS c, 'ana.f@shop.test' AS e, 'Ana Fielding' AS f
  UNION ALL SELECT 2, 'ben.c@shop.test',   'Ben Cortez'
  UNION ALL SELECT 3, 'carla.d@shop.test', 'Carla Dunn') AS v
WHERE NOT EXISTS (SELECT 1 FROM `Customer`);
