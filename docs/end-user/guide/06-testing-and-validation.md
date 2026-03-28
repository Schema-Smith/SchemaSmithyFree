# Testing and Validation

The best time to find a deployment problem is before it reaches production. A schema change that passes review but breaks on deploy wastes time, burns trust, and makes the next deployment scarier. SchemaSmith gives you multiple layers of defense — from local Docker testing to CI validation pipelines — so you catch issues early and deploy with confidence. This is the Strengthen pillar in action: fortifying your process so problems never reach production.

## Local testing with Docker

The demo included with SchemaSmith ships a Docker Compose file that stands up a complete environment from nothing — a SQL Server instance, databases created from scratch, and full schema deployments — all in one command. This is not just a convenience for trying the demo. It is a pattern you can adopt for your own projects.

```bash
docker compose -f demo/docker-compose.yml up
```

That single command does everything: starts SQL Server, waits for it to be healthy, deploys Northwind, then deploys AdventureWorks. When it finishes, you have two fully quenched databases running locally.

Here is how the compose file is structured:

**demoserver** starts a SQL Server container with a health check that polls readiness every five seconds. Nothing else starts until the server reports healthy. Environment variables configure the SA credentials, and the port maps to 1450 on the host so it does not collide with any existing SQL Server instance.

**quench-northwind** builds SchemaQuench from the project Dockerfile, mounts the Northwind schema package as a volume at `/metadata`, and deploys it to the demo server. All configuration flows through environment variables — target server, credentials, package path, and the `ProductDb` token that names the database. It depends on `demoserver` with a `service_healthy` condition, so it waits for SQL Server to be ready before attempting deployment.

**quench-adventureworks** follows the same pattern but depends on `quench-northwind` completing successfully first. This sequential dependency means the products deploy in order, just as they would in a real pipeline.

**completed** is a lightweight Alpine container that simply echoes "completed." It depends on all three services — healthy server, both products deployed — so it only runs when everything succeeds. If any step fails, `completed` never runs and the exit code tells you something broke.

The key patterns to carry into your own projects:

- **Environment variables configure everything.** Server, credentials, tokens, and package paths all flow through env vars, making the same compose file work across environments.
- **Health checks enforce readiness.** The server health check prevents SchemaQuench from connecting before SQL Server is ready to accept connections.
- **Volume-mounted packages.** The schema package is mounted into the container, not baked into the image. Change files on disk, run compose again, see the results.
- **Sequential dependencies.** `depends_on` with `condition: service_completed_successfully` guarantees deployment order.

The testing workflow becomes a tight loop: make changes to your schema files, run `docker compose up`, verify success, and tear down with `docker compose down -v` to reset completely. Every run starts from zero, which means you are testing the full deployment path — not just incremental changes against a database that might have drifted.

## Schema validation in CI

Before a schema package ever reaches a database, you can validate that every JSON file is structurally correct. The demo repository includes a GitHub Actions workflow that validates schema files on every pull request — catching malformed JSON, missing required properties, and structural errors without spinning up a database at all.

Here is the Northwind validation job from the workflow:

```yaml
validate-northwind:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4

    - name: validate-northwind-product
      uses: GrantBirki/json-yaml-validate@v3.3.0
      with:
        comment: "true"
        use_gitignore: "false"
        json_schema: "./demo/Northwind/.json-schemas/products.schema"
        files: "./demo/Northwind/Product.json"

    - name: validate-northwind-template
      uses: GrantBirki/json-yaml-validate@v3.3.0
      with:
        comment: "true"
        use_gitignore: "false"
        json_schema: "./demo/Northwind/.json-schemas/templates.schema"
        files: "./demo/Northwind/Templates/Northwind/Template.json"

    - name: validate-northwind-initialize-template
      uses: GrantBirki/json-yaml-validate@v3.3.0
      with:
        comment: "true"
        use_gitignore: "false"
        json_schema: "./demo/Northwind/.json-schemas/templates.schema"
        files: "./demo/Northwind/Templates/Initialize/Template.json"

    - name: validate-northwind-tables
      uses: GrantBirki/json-yaml-validate@v3.3.0
      with:
        comment: "true"
        use_gitignore: "false"
        json_schema: "./demo/Northwind/.json-schemas/tables.schema"
        base_dir: "./demo/Northwind/Templates/Northwind/Tables"
```

Each schema package includes JSON Schema files in a `.json-schemas/` directory. The workflow validates three categories:

- **Product definition** — `Product.json` validated against `products.schema`. Catches missing product names, invalid token structures, malformed validation scripts.
- **Template definitions** — Each `Template.json` validated against `templates.schema`. Catches invalid template order entries, broken database identification scripts, malformed settings.
- **Table definitions** — Every table JSON file in the `Tables/` directory validated against `tables.schema`. Catches invalid column types, malformed index definitions, structural errors in any table.

The `comment: "true"` setting posts validation results directly on the pull request, so reviewers see exactly what failed without digging through logs.

This is the first line of defense. No database, no deployment, no credentials required — just structural validation that runs in seconds. A typo in a column definition or a missing required field gets caught here, long before it could cause a deployment failure.

## WhatIf as a validation gate

Schema validation catches structural problems in your JSON files. But valid JSON can still produce invalid SQL. A column referencing a type that does not exist, a foreign key pointing to a table that was renamed, a token that was never defined — these pass schema validation but fail at deployment time. WhatIf mode catches them.

WhatIf runs the full SchemaQuench deployment logic — validation scripts, token replacement, SQL generation, dependency resolution — without executing any changes against the database. It is a complete dry run.

```bash
SmithySettings_WhatIfONLY=true SchemaQuench
```

In WhatIf mode, SchemaQuench:

- **Executes validation scripts normally.** Server validation and baseline validation still run, because they are read-only checks that need real answers.
- **Generates table quench SQL without applying it.** The SQL that would create, alter, or drop tables is generated and logged but never executed.
- **Reports migration script status.** For each migration script, WhatIf reports whether it would be applied or skipped (because it was already tracked in a previous deployment).

The output tells you exactly what SchemaQuench would do — every table change, every script execution, every migration — without touching a single row.

The pattern for PR pipelines: spin up a disposable database container, deploy the base branch schema to establish the current state, then run SchemaQuench in WhatIf mode against the PR branch. If WhatIf succeeds, the PR is deployable. If it fails, the PR check fails and the author knows exactly what broke. This catches real SQL execution issues, not just JSON structure problems.

For the full details on WhatIf output, debug SQL files, and configuration options, see the [SchemaQuench Reference -- WhatIf Mode](../reference/schemaquench.md#whatif-mode).

## Validation scripts as deployment gates

The final layer runs at deployment time itself. The `ValidationScript` property in `Product.json` executes before SchemaQuench deploys anything. It is your safety gate: if the script returns 0 or false, deployment stops. This prevents accidentally quenching to the wrong server or an unprepared environment.

**Verify the target database exists:**

```sql
SELECT CAST(CASE WHEN EXISTS(
    SELECT * FROM master.dbo.sysdatabases WHERE [name] = '{{MainDB}}'
) THEN 1 ELSE 0 END AS BIT)
```

**Verify a minimum SQL Server version:**

```sql
SELECT CAST(CASE WHEN SERVERPROPERTY('ProductMajorVersion') >= 15
    THEN 1 ELSE 0 END AS BIT)
```

**Confirm expected state before a migration:**

```sql
SELECT CAST(CASE WHEN EXISTS(
    SELECT * FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'AppConfig' AND TABLE_SCHEMA = 'dbo'
) THEN 1 ELSE 0 END AS BIT)
```

Validation scripts support token replacement, so you can use `{{ProductName}}`, `{{MainDB}}`, or any custom token you have defined. If validation fails, SchemaQuench logs the failure and exits without modifying anything. Nothing touched. Nothing broken. Exactly how a safety gate should work.

Products also support `BaselineValidationScript`, which runs only during initial baseline deployments to verify the target is in the expected starting state.

### Three layers, three classes of problems

Each validation layer catches problems the others cannot:

| Layer | What it catches | When it runs | Database required? |
|-------|----------------|--------------|-------------------|
| **JSON Schema validation** | Malformed JSON, missing properties, structural errors | PR time | No |
| **WhatIf mode** | Invalid SQL, missing tokens, dependency failures | PR or pre-deploy | Yes (disposable) |
| **Validation scripts** | Wrong server, wrong state, wrong version | Deploy time | Yes (target) |

Schema validation is fast and cheap — run it on every PR. WhatIf is thorough but needs a database — run it on PRs that touch schema files. Validation scripts are your last line of defense — they run on every deployment, every time, automatically. Together, they form a safety net that catches problems at the earliest possible moment, keeping them far from production.

---

Testing and validation give you confidence that your schema changes will deploy correctly. The next chapter shows how to wire these checks into your CI/CD pipeline so they run automatically on every change. [CI/CD Integration](07-cicd-integration.md)
