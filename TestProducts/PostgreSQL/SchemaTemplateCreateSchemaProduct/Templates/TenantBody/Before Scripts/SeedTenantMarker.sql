-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Inserts a per-tenant marker into the iteration's customers table. Migration
-- tracking ((template, schema) tuple) should record one row PER tenant after the
-- first quench, and skip this script entirely on a second quench.
-- Before-slot scripts run after table creation but before PK / index creation, so
-- ON CONFLICT (customer_id) isn't yet available. NOT EXISTS guard instead.
INSERT INTO "{{SchemaName}}".customers (customer_id, marker)
  SELECT 1, '{{SchemaName}}_marker'
  WHERE NOT EXISTS (SELECT 1 FROM "{{SchemaName}}".customers WHERE customer_id = 1);
