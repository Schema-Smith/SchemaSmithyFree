-- Prod-only seed: gives ordersservice_prod real OrderHeader rows the rename must not lose.
INSERT INTO public."Customer" ("Name", "Email", "LoyaltyTier") VALUES
  ('Ada Lovelace',   'ada@example.com',   'Gold'),
  ('Alan Turing',    'alan@example.com',  'Standard'),
  ('Grace Hopper',   'grace@example.com', 'Gold');
INSERT INTO public."OrderHeader" ("CustomerId", "OrderDate", "TotalAmount") VALUES
  (1, '2026-01-15 10:00:00', 129.99),
  (1, '2026-02-03 14:30:00',  49.50),
  (2, '2026-02-11 09:15:00', 799.00),
  (3, '2026-03-01 16:45:00',  12.00);
