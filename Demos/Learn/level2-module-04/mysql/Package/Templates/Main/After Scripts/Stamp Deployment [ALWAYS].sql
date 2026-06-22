-- Stamps this run's resolved token values into DeploymentLog. The [ALWAYS] tag re-runs the
-- script every quench (it is never tracked as a completed migration). MySQL forbids referencing
-- an INSERT's target table in a subquery, so idempotency rides on INSERT IGNORE + the Environment
-- primary key: a second run for the same environment is silently ignored. Every value below
-- arrives by token: {{ProductName}} is automatic, {{EngineVersion}} is computed live against this
-- server by a <*Query*> token, and {{Environment}} / {{ReleaseVersion}} come from whichever
-- settings file drove this run.
INSERT IGNORE INTO `DeploymentLog` (`Environment`, `ProductName`, `ReleaseVersion`, `EngineVersion`)
VALUES ('{{Environment}}', '{{ProductName}}', '{{ReleaseVersion}}', '{{EngineVersion}}');
