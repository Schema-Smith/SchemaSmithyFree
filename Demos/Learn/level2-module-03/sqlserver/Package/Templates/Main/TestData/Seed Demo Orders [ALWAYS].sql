-- Demo/sample order rows for non-production environments. This whole folder is gated by the
-- ShouldApplyExpression on its ScriptFolders entry (Environment = Development), so on a Production
-- target the folder is skipped entirely and these rows never land. [ALWAYS] re-runs the seed every
-- quench; WHERE NOT EXISTS keeps it idempotent.
INSERT INTO [dbo].[Orders] ([OrderId], [Region])
SELECT v.[OrderId], v.[Region]
  FROM (VALUES (1001, N'North'), (1002, N'South'), (1003, N'West')) AS v([OrderId], [Region])
 WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Orders] o WHERE o.[OrderId] = v.[OrderId]);
