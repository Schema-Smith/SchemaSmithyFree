# SchemaSmith Community Documentation

Welcome to the shop. Everything you need to master SchemaSmith is here -- the guide walks you through it front to back, and the reference docs are always within arm's reach when you need the details. Pick a starting point and dig in.

---

## Start Here

New to SchemaSmith? The guide takes you from "what is this?" to confidently managing databases. Start at the beginning and follow the thread:

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

Already know what you're looking for? Jump straight to the details.

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

Nothing beats working with real data. The repository includes two complete demo products you can extract, deploy, and explore:

- **[Northwind](../../demo/Northwind/)** — 13 tables, ideal for getting started
- **[AdventureWorks](../../demo/AdventureWorks/)** — 71 tables, real-world complexity

Both were extracted and deployed end-to-end with SchemaSmith tools.
