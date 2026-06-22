-- Demo/sample order rows for non-production environments. This whole folder is gated by the
-- ShouldApplyExpression on its ScriptFolders entry (Environment = Development), so on a Production
-- target the folder is skipped entirely and these rows never land. [ALWAYS] re-runs the seed every
-- quench; WHERE NOT EXISTS keeps it idempotent.
INSERT INTO public.orders (orderid, region)
SELECT v.orderid, v.region
  FROM (VALUES (1001, 'North'), (1002, 'South'), (1003, 'West')) AS v(orderid, region)
 WHERE NOT EXISTS (SELECT 1 FROM public.orders o WHERE o.orderid = v.orderid);
