# SchemaSmith Community Documentation

SchemaSmith is a state-based schema management toolset for SQL Server. Declare what your database should look like — tables as JSON, programmable objects as SQL — and let the tools handle extraction, review, deployment, and data management.

---

## Start Here

New to SchemaSmith? Start with the guide and follow it front to back:

**[Why SchemaSmith →](guide/01-why-schemasmith.md)**

---

## The Guide

A hands-on journey from first contact to power usage.

| Chapter | What you'll learn |
|---------|-------------------|
| [1. Why SchemaSmith](guide/01-why-schemasmith.md) | The problem, the vision, and why state-based changes everything |
| [2. Quick Start](guide/02-quick-start.md) | Extract, view, deploy, and change a database in under 15 minutes |
| [3. Core Concepts](guide/03-core-concepts.md) | Products, templates, schema packages, and the deployment model |
| [4. Day-to-Day Workflows](guide/04-day-to-day-workflows.md) | Adding tables, modifying schemas, team collaboration, source control |
| [5. Power Workflows](guide/05-power-workflows.md) | Tokens, multi-database products, CI/CD, reference data, extraction intelligence |
| [6. Edge Cases & Escape Hatches](guide/06-edge-cases.md) | Migration scripts, renames, cross-dependencies, special types |
| [7. Troubleshooting](guide/07-troubleshooting.md) | Common issues, log reading, diagnostics |

---

## Reference

Jump to the details you need.

| Reference | Covers |
|-----------|--------|
| [SchemaTongs](reference/schematongs.md) | Schema extraction — all 13 object types, config, orphan detection, validation |
| [SchemaQuench](reference/schemaquench.md) | Deployment engine — execution flow, slots, WhatIf, migration tracking |
| [SchemaHammer](reference/schemahammer.md) | Desktop viewer — navigation, property editors, search, token preview |
| [DataTongs](reference/datatongs.md) | Data extraction — MERGE generation, type handling, key detection |
| [Configuration](reference/configuration.md) | Shared CLI switches, config hierarchy, environment variables, logging |
| [Schema Packages](reference/schema-packages.md) | Product/Template JSON, folder structure, table definition format |
| [Script Tokens](reference/script-tokens.md) | Token syntax, resolution order, overrides, environment variables |

---

## Demo Products

The repository includes two complete demo products for hands-on exploration:

- **[Northwind](../../demo/Northwind/)** — 13 tables, ideal for getting started
- **[AdventureWorks](../../demo/AdventureWorks/)** — 71 tables, real-world complexity

Both were extracted and deployed end-to-end with SchemaSmith tools.
