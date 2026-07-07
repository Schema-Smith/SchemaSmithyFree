-- Backfill: onboard customers migrated from the legacy CRM, in two batches.
-- Batch 1 tidies existing display names (idempotent). Batch 2 inserts the migrated
-- customers as a single statement — but the last row is missing its email (a NOT NULL
-- column), so the whole INSERT fails atomically and inserts nothing: the FAILING BATCH (#2).
UPDATE dbo.Customer SET [FullName] = LTRIM(RTRIM([FullName])) WHERE [FullName] IS NOT NULL;
GO
INSERT dbo.Customer ([CustomerId],[Email],[FullName]) VALUES
  (10, 'devon.p@shop.test', 'Devon Price'),
  (11, 'erin.k@shop.test',  'Erin Knox'),
  (12, 'farah.n@shop.test', 'Farah Nasser'),
  (13, NULL,                'Gil Overton');   -- Email is NOT NULL -> whole INSERT fails
GO
