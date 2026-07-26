-- Prod-only seed: gives ordersservice_prod real OrderHeader rows the rename must not lose.
INSERT INTO dbo.Customer (Name, Email, LoyaltyTier) VALUES
  (N'Ada Lovelace',   N'ada@example.com',   N'Gold'),
  (N'Alan Turing',    N'alan@example.com',  N'Standard'),
  (N'Grace Hopper',   N'grace@example.com', N'Gold');
INSERT INTO dbo.OrderHeader (CustomerId, OrderDate, TotalAmount) VALUES
  (1, '2026-01-15T10:00:00', 129.99),
  (1, '2026-02-03T14:30:00',  49.50),
  (2, '2026-02-11T09:15:00', 799.00),
  (3, '2026-03-01T16:45:00',  12.00);
