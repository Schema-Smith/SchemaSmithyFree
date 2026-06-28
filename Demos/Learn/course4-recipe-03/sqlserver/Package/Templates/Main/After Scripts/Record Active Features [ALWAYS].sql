-- [ALWAYS] runs on every quench (not a tracked run-once migration), so each deploy records the
-- feature set that was live on the server at that moment. {{EnabledFeatures}} is resolved by the
-- <*Query*> token in Product.json, executed against THIS target just before this script runs.
INSERT INTO dbo.DeployLog (ActiveFeatures) VALUES (N'{{EnabledFeatures}}');
