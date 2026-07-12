-- Undo the staged failure: clear the duplicate emails so UQ_Customer_Email can build.
-- Re-run the after deploy and fleet_tenant_003 converges too — a clean, all-Success summary.
USE fleet_tenant_003;
DELETE FROM dbo.Customer WHERE Email = N'dupe@shop.example';
