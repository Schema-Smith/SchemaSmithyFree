-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Inserts a per-tenant marker into the iteration's Customers table. Migration
-- tracking ((template, schema) tuple) should record one row PER tenant after the
-- first quench, and skip this script entirely on a second quench.
IF NOT EXISTS (SELECT 1 FROM [{{SchemaName}}].[Customers] WHERE CustomerID = 1)
    INSERT [{{SchemaName}}].[Customers] (CustomerID, Marker) VALUES (1, '{{SchemaName}}_marker');
