-- Seeds the tenant registry. The [ALWAYS] tag re-runs this every quench (it is never
-- tracked as a completed migration), and the WHERE NOT EXISTS guard keeps it idempotent:
-- a tenant that already exists is left untouched. This is the registry that the
-- TenantWorkspace schema template reads to decide which workspaces to fan out into.

INSERT INTO public.tenants (name, display_name)
SELECT v.name, v.display_name
  FROM (VALUES
      ('acme',   'Acme Corporation'),
      ('beta',   'Beta Industries'),
      ('globex', 'Globex LLC')
  ) AS v (name, display_name)
 WHERE NOT EXISTS (
      SELECT 1 FROM public.tenants t WHERE t.name = v.name
 );
