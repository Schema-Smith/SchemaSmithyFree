-- Baseline seed for the script-slot + data-delivery sandbox. Three valid customers with
-- unique emails; SalesOrder is left empty (beat 2 delivers into it). Run-once: tracked in
-- CompletedMigrationScripts, seeds on the baseline deploy, never re-runs.
INSERT INTO public.customer (customerid, email, fullname)
SELECT * FROM (VALUES
  (1, 'ana.f@shop.test',   'Ana Fielding'),
  (2, 'ben.c@shop.test',   'Ben Cortez'),
  (3, 'carla.d@shop.test', 'Carla Dunn')
) AS v(customerid, email, fullname)
WHERE NOT EXISTS (SELECT 1 FROM public.customer);
