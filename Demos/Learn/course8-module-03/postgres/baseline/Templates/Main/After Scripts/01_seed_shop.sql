-- Seed the diag_keys baseline. Two arming conditions for Module 3's two beats:
--   Beat 1 (index): customerid 1 and 7 share an email — harmless under the non-unique
--     ix_customer_email, but the dirty data the UNIQUE flip trips on.
--   Beat 2 (FK): salesorder orderid 3 references customerid 999, which has no customer
--     row — a resident orphan. Harmless while salesorder carries no FK, but the row the
--     fk_salesorder_customer add validates against (and fails on) in beat 2.
-- Run-once (no [ALWAYS]): tracked in CompletedMigrationScripts, so it seeds on the
-- baseline deploy and never re-runs — which is what lets the manual data fixes stick.
INSERT INTO public.customer (customerid, email, fullname)
SELECT * FROM (VALUES
  (1, 'ana.f@shop.test',   'Ana Fielding'),
  (2, 'ben.c@shop.test',   'Ben Cortez'),
  (3, 'carla.d@shop.test', 'Carla Dunn'),
  (7, 'ana.f@shop.test',   'Ana Fielding-Reyes')
) AS v(customerid, email, fullname)
WHERE NOT EXISTS (SELECT 1 FROM public.customer);

INSERT INTO public.salesorder (orderid, customerid, orderdate, status)
SELECT * FROM (VALUES
  (1, 1,   TIMESTAMP '2026-01-05', 'Shipped'),
  (2, 2,   TIMESTAMP '2026-01-06', 'Shipped'),
  (3, 999, TIMESTAMP '2026-01-07', 'Open')
) AS v(orderid, customerid, orderdate, status)
WHERE NOT EXISTS (SELECT 1 FROM public.salesorder);
