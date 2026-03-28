# SchemaSmith Community Documentation

Welcome to the shop. Everything you need to master SchemaSmith is here -- the guide walks you through it front to back, and the reference docs are always within arm's reach when you need the details. Pick a starting point and dig in.

---

## Start Here

New to SchemaSmith? The guide takes you from "what is this?" to confidently managing databases. Start at the beginning and follow the thread:

**[Why SchemaSmith →](guide/01-why-schemasmith.md)**

---

## The Guide

A hands-on journey from first contact to confident production deployment, organized around three pillars: **Shape** your schema, **Strengthen** your process, **Succeed** in production.

### Prologue

| Chapter | What you'll learn |
|---------|-------------------|
| [1. Why SchemaSmith](guide/01-why-schemasmith.md) | The problem, the vision, and why state-based changes everything |
| [2. Quick Start](guide/02-quick-start.md) | Extract, view, deploy, and change a database in under 15 minutes |

### Shape -- Give your database form

| Chapter | What you'll learn |
|---------|-------------------|
| [3. Core Concepts](guide/03-core-concepts.md) | Products, templates, schema packages, and the deployment model |
| [4. Defining Your Schema](guide/04-defining-your-schema.md) | Adding tables, modifying schemas, extraction, the Initialize pattern |

### Strengthen -- Fortify your process

| Chapter | What you'll learn |
|---------|-------------------|
| [5. Working with Your Team](guide/05-working-with-your-team.md) | Source control patterns, code review, team collaboration |
| [6. Testing and Validation](guide/06-testing-and-validation.md) | Docker testing, CI validation, WhatIf as safety net |
| [7. CI/CD Integration](guide/07-cicd-integration.md) | Pipeline examples, env var config, secrets, build/deploy model |
| [8. Rollback and Recovery](guide/08-rollback-and-recovery.md) | What rolls back automatically, procedures, best practices |

### Succeed -- Deploy with confidence

| Chapter | What you'll learn |
|---------|-------------------|
| [9. Power Workflows](guide/09-power-workflows.md) | Script tokens, multi-database products, DataTongs, execution slots |
| [10. Edge Cases & Escape Hatches](guide/10-edge-cases.md) | Migration scripts, renames, cross-dependencies, special types |

### Appendix

| Chapter | What you'll learn |
|---------|-------------------|
| [11. Troubleshooting](guide/11-troubleshooting.md) | Common issues, log reading, diagnostics |

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
