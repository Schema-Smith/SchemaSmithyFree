-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Before-slot scripts run after table creation but before PK / index creation, so
-- ON CONFLICT (lookup_id) isn't yet available. Use an explicit NOT EXISTS guard
-- (still idempotent across reruns even with PruneObsoleteMigrationTracking on).
INSERT INTO public.lookup (lookup_id, code)
  SELECT 1, 'ALPHA'
  WHERE NOT EXISTS (SELECT 1 FROM public.lookup WHERE lookup_id = 1);
