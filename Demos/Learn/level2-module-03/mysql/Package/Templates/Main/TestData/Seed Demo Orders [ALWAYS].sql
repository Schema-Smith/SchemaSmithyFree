-- Demo/sample order rows for non-production environments. This whole folder is gated by the
-- ShouldApplyExpression on its ScriptFolders entry (Environment = Development), so on a Production
-- target the folder is skipped entirely and these rows never land. [ALWAYS] re-runs the seed every
-- quench; INSERT IGNORE + the OrderId primary key keep it idempotent (MySQL forbids referencing an
-- insert's target table in a subquery, so the WHERE NOT EXISTS guard the other engines use isn't
-- available here).
INSERT IGNORE INTO `Orders` (`OrderId`, `Region`)
VALUES (1001, 'North'), (1002, 'South'), (1003, 'West');
