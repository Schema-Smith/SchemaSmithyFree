-- Stage the failure on ONE tenant: plant duplicate emails so the new UQ_Customer_Email
-- unique index cannot build. The other four tenants have no duplicates and succeed.
USE fleet_tenant_003;
DELETE FROM dbo.Customer;
INSERT INTO dbo.Customer (CustomerId, Email, FullName)
VALUES (1, N'dupe@shop.example', N'Ada Lovelace'),
       (2, N'dupe@shop.example', N'Alan Turing');
