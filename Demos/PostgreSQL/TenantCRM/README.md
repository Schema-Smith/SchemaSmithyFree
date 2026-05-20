# TenantCRM — Multi-Tenant CRM Demo (PostgreSQL)

A miniature multi-tenant CRM that showcases the **schema-per-tenant** pattern: one
schema-template definition fanned out across an arbitrary number of tenant
schemas inside a single database. New tenants are onboarded by calling a stored
procedure; new releases re-deploy every tenant's schema from the same source
package; hotfixes can target a single tenant for canary rollout.

## What this demo shows

- A `Shared` template (regular) that owns the tenant directory, the global audit
  log, the per-tenant plan lookup, and a `public.onboard_tenant` procedure.
- A `TenantWorkspace` schema template — one `Template.json` deploys the same
  customers / contacts / activities / activity_types shape into every tenant's
  own schema.
- Cross-schema foreign keys — `customers.country_code` → `public.countries.code` —
  so per-tenant data references shared dimension tables without duplication.
- Per-tenant migration tracking — a Before-slot migration backfills countries,
  tracked once per `(template, tenant)` in
  `SchemaSmith.completed_migration_scripts` so a re-quench skips it.
- DataDelivery in a schema template — `activity_types` is seeded via a
  `.tabledata` file with `MergeType: "Insert"`, so each tenant gets the default
  set the first time their schema deploys and tenant-specific additions or
  renames after the seed are left alone.
- The canonical onboarding pattern — `public.onboard_tenant` runs `CREATE SCHEMA`
  + `INSERT public.tenants` atomically, pairing with `CreateSchemaIfMissing: false`
  to keep tenant rows and tenant schemas in sync.
- Selective execution scope — deploy a hotfix to a single tenant first with
  `Target.Schemas: ["tenant_acme"]` before rolling it out unrestricted.

## Prerequisites

- Docker (for the local PostgreSQL container) or any PostgreSQL 15+ instance.
- The SchemaSmith CLI (or use the bundled `docker compose up` flow from the
  parent `Demos/PostgreSQL/` directory).

A self-contained way to run this demo:

```bash
cd Demos/PostgreSQL
docker compose pull
docker compose up
```

The compose chain spins up a PostgreSQL container and quenches the demo products
in sequence. TenantCRM lands after the standard sample databases.

## Deploy it

From a checkout of the repo, with SchemaQuench on `PATH`:

```bash
schemaquench \
  --SchemaPackagePath ./Demos/PostgreSQL/TenantCRM \
  --Target:Server localhost \
  --Target:User postgres \
  --Target:Password <yourpassword>
```

Expected log highlights (truncated):

```
Quenching Template: Initialize
[localhost].[tenantcrm] Successfully Quenched
Quenching Template: Shared
[localhost].[tenantcrm] Successfully Quenched
Quenching Template: TenantWorkspace
[localhost].[tenantcrm] [Schema: tenant_acme] Begin Quench
[localhost].[tenantcrm] [Schema: tenant_acme] Successfully Quenched
[localhost].[tenantcrm] [Schema: tenant_beta] Begin Quench
[localhost].[tenantcrm] [Schema: tenant_beta] Successfully Quenched
Completed quench of TenantCRM
```

The `[Schema: <tenant>]` prefix is the iteration marker — every line that scopes
to a particular tenant carries it. `Shared` runs once; `TenantWorkspace` runs
once per row returned by its `SchemaIdentificationScript`. This demo ships with
`AllowParallel: false` on the PostgreSQL `TenantWorkspace` template (the SQL
Server demo runs tenants in parallel). Cross-schema FK contention on a
PostgreSQL shared dimension table can deadlock parallel iterations; serial is
the safe default for the cross-schema FK pattern. Flip to `true` if your
tenants don't share a referenced table.

Initial deploy creates two empty schemas because `public.tenants` is empty. To
see real per-tenant work, onboard tenants first (next section) and re-quench.

## Inspect the result

After the first quench (with tenants onboarded):

```sql
-- All tenants and their plan
SELECT t.name, t.display_name, p.plan_name, t.status, t.created_at
  FROM public.tenants t
  JOIN public.plans p ON p.plan_id = t.plan_id
 ORDER BY t.name;

-- Per-tenant table structure (replace tenant_acme with any tenant)
SELECT table_schema, table_name
  FROM information_schema.tables
 WHERE table_schema = 'tenant_acme'
 ORDER BY table_name;

-- Migration tracking per tenant
SELECT template_name, schema_name, script_path, completed_at
  FROM "SchemaSmith".completed_migration_scripts
 WHERE product_name = 'TenantCRM'
 ORDER BY template_name, schema_name, script_path;
```

## Onboard a new tenant

The canonical pattern — `CALL public.onboard_tenant(...)` creates the tenant
schema and the tenant row atomically:

```sql
\c tenantcrm
CALL public.onboard_tenant('tenant_acme', 'Acme Corporation', 2);  -- Pro
CALL public.onboard_tenant('tenant_beta', 'Beta Industries', 1);    -- Free
```

Then re-quench. `TenantWorkspace` discovers both tenants and runs the full
template iteration for each one — tables, procedures, function, view, trigger
function, trigger, the `Migration_001_BackfillCountries.sql` Before-slot
migration, and the `activity_types` data delivery.

## Roll out a hotfix to one tenant first

Edit a procedure (say, `record_activity.sql`) and quench with `Target.Schemas`
narrowed to one tenant:

```bash
schemaquench \
  --SchemaPackagePath ./Demos/PostgreSQL/TenantCRM \
  --Target:Server localhost \
  --Target:Schemas:0 tenant_acme
```

Only `tenant_acme` sees the new procedure. The other tenants are skipped — no
log lines, no schema iteration, no migration tracking writes. Once the canary
holds up in production, drop the `Target.Schemas` arg and re-quench unrestricted
to fan out to every tenant.

## Observe per-tenant migration tracking

Run the deploy twice. After the first run, `completed_migration_scripts` has
one row per `(TenantWorkspace, tenant)` for `Migration_001_BackfillCountries`.
After the second run, that row still holds and the migration skips silently.
`activity_types` is delivered via DataDelivery (`Insert` merge type), so a
re-quench reconciles missing seed rows but leaves any tenant-added rows alone —
no migration tracking involved. Verify:

```sql
-- After first quench: 1 row per tenant (Migration_001_BackfillCountries)
SELECT schema_name, COUNT(*) AS migrations_completed
  FROM "SchemaSmith".completed_migration_scripts
 WHERE product_name = 'TenantCRM' AND template_name = 'TenantWorkspace'
 GROUP BY schema_name
 ORDER BY schema_name;
```

Onboard a third tenant and re-quench. The new tenant's migration runs and its
`activity_types` get seeded; the existing tenants' tracking rows stay put and
the migration skips.

## Caveats

> **Note:** `CreateSchemaIfMissing: false` means the discovery script lists
> tenants whose schemas already exist. Insert a tenant row without first calling
> `public.onboard_tenant` and the next quench will abort that iteration with a
> clear error pointing at the three onboarding paths.

> **Warning:** Cross-schema FK refs (`customers.country_code` →
> `public.countries.code`) require the referenced row to exist before tenant data
> lands. The `countries.tabledata` seed in `Shared` runs once on every quench
> before `TenantWorkspace`, which guarantees the reference is satisfied.

> **MySQL:** Schema templates are SQL Server and PostgreSQL only — MySQL doesn't
> have a schema-inside-database concept that matches the pattern. For MySQL
> multi-tenancy, use database-per-tenant: one database per tenant, the
> `DatabaseIdentificationScript` returning every tenant database name.

> **Tip:** `Target.Schemas` filters the iteration without affecting tracking
> semantics. A tenant skipped via `Target.Schemas` is genuinely skipped — no
> rows written, no migrations tracked, no log noise — so canary deploys leave
> the rest of your tenants exactly as they were.

> **Note:** The `ContinueOnSchemaFailure: true` setting on `TenantWorkspace`
> means if one tenant's iteration errors (a hand-edited procedure with a syntax
> error, for example), the other tenants still deploy. The failed tenant
> surfaces a per-iteration error; the rest succeed.

## What's not shown

- Failure isolation across templates (`ContinueOnDatabaseFailure`). The demo
  has one database; this setting matters when your product spans multiple.
- DataTongs schema-template data extraction. The `public.plans`,
  `public.countries`, and per-tenant `activity_types` seeds are hand-written
  `.tabledata` files; in a real product, DataTongs would extract per-tenant
  data with `Source.Schema` set to one tenant's schema.
- Tenant offboarding workflow. Setting `public.tenants.status = 'Inactive'`
  drops the tenant from the discovery script's output; its schema and data are
  preserved on disk but no longer touched by quenches. Cleanup is intentionally
  left to operations tooling.

The full Multi-Tenant Deployments guide chapter walks these scenarios in
context.
