-- fleet_tenant_002 drift: two customers share an email. When the fleet rolls out the
-- new UQ_Customer_Email unique index, this tenant's index build fails (1505) at
-- the indexes-and-constraints phase, while the rest of the fleet hardens it fine.
USE [fleet_tenant_002];
DELETE FROM dbo.OrderItem; DELETE FROM dbo.SalesOrder; DELETE FROM dbo.Customer;
INSERT INTO dbo.Customer (CustomerId, Email, FullName) VALUES
  (1, 'dupe@shop.example', 'Ada Byte'),
  (2, 'dupe@shop.example', 'Grace Stack');
