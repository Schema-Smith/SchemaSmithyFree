-- fleet_tenant_002 drift: two customers share an email. When the fleet rolls out the
-- new UQ_Customer_Email unique index, this tenant's index build fails (MySQL 1062) at
-- the indexes phase, while the rest of the fleet hardens it fine.
USE fleet_tenant_002;
DELETE FROM OrderItem; DELETE FROM SalesOrder; DELETE FROM Customer;
INSERT INTO Customer (CustomerId, Email, FullName) VALUES
  (1, 'dupe@shop.example', 'Ada Byte'),
  (2, 'dupe@shop.example', 'Grace Stack');
