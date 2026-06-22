-- Seeds the tenant registry. The [ALWAYS] tag re-runs this every quench (it is never
-- tracked as a completed migration), and the WHERE NOT EXISTS guard keeps it idempotent:
-- a tenant that already exists is left untouched. This is the registry that the
-- TenantWorkspace schema template reads to decide which workspaces to fan out into.

INSERT INTO [dbo].[Tenants] ([Name], [DisplayName])
SELECT v.[Name], v.[DisplayName]
  FROM (VALUES
      (N'acme',   N'Acme Corporation'),
      (N'beta',   N'Beta Industries'),
      (N'globex', N'Globex LLC')
  ) AS v ([Name], [DisplayName])
 WHERE NOT EXISTS (
      SELECT 1 FROM [dbo].[Tenants] t WHERE t.[Name] = v.[Name]
 );
