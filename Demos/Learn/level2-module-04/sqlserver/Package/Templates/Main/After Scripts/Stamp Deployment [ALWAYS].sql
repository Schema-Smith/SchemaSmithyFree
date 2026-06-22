-- Stamps this run's resolved token values into DeploymentLog. The [ALWAYS] tag re-runs the
-- script every quench (it is never tracked as a completed migration); the WHERE NOT EXISTS guard
-- keeps it idempotent per environment. Every value below arrives by token: {{ProductName}} is
-- automatic, {{EngineVersion}} is computed live against this server by a <*Query*> token, and
-- {{Environment}} / {{ReleaseVersion}} come from whichever settings file drove this run.
INSERT INTO [dbo].[DeploymentLog] ([Environment], [ProductName], [ReleaseVersion], [EngineVersion])
SELECT N'{{Environment}}', N'{{ProductName}}', N'{{ReleaseVersion}}', N'{{EngineVersion}}'
 WHERE NOT EXISTS (
     SELECT 1 FROM [dbo].[DeploymentLog] WHERE [Environment] = N'{{Environment}}'
 );
