-- Backfill: onboard customers migrated from the legacy CRM, in two statements.
-- Statement 1 tidies existing display names (idempotent). Statement 2 inserts the migrated
-- customers as a single INSERT — but the last row is missing its email (a NOT NULL column),
-- so the whole INSERT fails atomically and inserts nothing.
-- (MySQL scripts are split on ';', not the SQL Server 'GO' separator; the whole script runs
-- as one batch, so the failing-batch marker reads (#1).)
UPDATE `Customer` SET `FullName` = TRIM(`FullName`) WHERE `FullName` IS NOT NULL;

INSERT INTO `Customer` (`CustomerId`, `Email`, `FullName`) VALUES
  (10, 'devon.p@shop.test', 'Devon Price'),
  (11, 'erin.k@shop.test',  'Erin Knox'),
  (12, 'farah.n@shop.test', 'Farah Nasser'),
  (13, NULL,                'Gil Overton');
