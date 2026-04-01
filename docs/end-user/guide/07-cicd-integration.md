# CI/CD Integration

SchemaSmith tools are self-contained executables. No SDK to install. No runtime to configure. No package manager plugins to maintain. Drop them into any pipeline and your database deployments become as automated as your application builds. One binary, a handful of environment variables, and your schema changes flow from pull request to production without anyone writing a deployment script.

## The build and deploy model

SchemaSmith separates schema management into two clean stages that map directly to how CI/CD pipelines already work.

**Build: package your schema into a versioned artifact.** Your schema package — `Product.json`, templates, table definitions, scripts — is already a directory structure in source control. Zip it, tag a release, publish it to an artifact store. The package is the artifact. No compilation step, no transformation, no intermediate format.

**Deploy: point SchemaQuench at the artifact and run it.** SchemaQuench reads everything it needs from the schema package and the environment. Set the target server, credentials, and package path via environment variables, then execute. One command. Done.

SchemaQuench reads directly from zip files — no extraction step needed. Build the artifact once, store it in your artifact repository, and deploy the same artifact to dev, staging, and production. The only thing that changes between environments is the configuration injected through environment variables. Same artifact, every environment, every time.

## Configuration via environment variables

Every SchemaSmith setting can be injected through environment variables, making the tools pipeline-native from the start. The convention is straightforward: prefix with `SmithySettings_` and use double underscores (`__`) to represent nesting in the configuration hierarchy.

| Setting path | Environment variable |
|---|---|
| `Target:Server` | `SmithySettings_Target__Server` |
| `Target:User` | `SmithySettings_Target__User` |
| `Target:Password` | `SmithySettings_Target__Password` |
| `SchemaPackagePath` | `SmithySettings_SchemaPackagePath` |
| `WhatIfONLY` | `SmithySettings_WhatIfONLY` |
| `ScriptTokens:ReportingDB` | `SmithySettings_ScriptTokens__ReportingDB` |

No secrets in config files. Every sensitive value — server addresses, credentials, tokens that vary by environment — injected at runtime by your pipeline's secret management system. The settings file in your repository holds only development defaults; production configuration lives where it belongs, in your CI/CD platform's secret store.

For the full mapping rules, precedence chain, and every available setting, see the [Configuration Reference](../reference/configuration.md#environment-variables).

## Pipeline examples

The examples below show complete, working pipelines for four major CI/CD platforms. Each one checks out the schema package, injects credentials from the platform's secret store, and runs SchemaQuench. That's the entire deployment.

### GitHub Actions

```yaml
name: Deploy Database Schema

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  deploy:
    runs-on: self-hosted
    steps:
      - name: Checkout schema package
        uses: actions/checkout@v4

      - name: Deploy Schema
        env:
          SmithySettings_SchemaPackagePath: ${{ github.workspace }}
          SmithySettings_Target__Server: ${{ secrets.DB_SERVER }}
          SmithySettings_Target__User: ${{ secrets.DB_USER }}
          SmithySettings_Target__Password: ${{ secrets.DB_PASSWORD }}
        run: schemaquench
```

This uses a self-hosted runner with SchemaQuench pre-installed. Credentials flow from GitHub Repository Secrets — never stored in the workflow file, never printed in logs. The `workflow_dispatch` trigger lets you run deployments manually when needed.

### Jenkins

```groovy
pipeline {
    agent any

    environment {
        SmithySettings_SchemaPackagePath = '/opt/artifacts/product-definition.zip'
        SmithySettings_Target__Server    = credentials('db-server')
        SmithySettings_Target__User      = credentials('db-user')
        SmithySettings_Target__Password  = credentials('db-password')
    }

    stages {
        stage('Deploy Schema') {
            steps {
                sh 'schemaquench'
            }
        }
    }
}
```

Jenkins injects credentials through the Credentials Plugin. Notice the package path points to a zip file — SchemaQuench reads directly from zip archives, so there's no extraction step. Build a zip artifact upstream, pass the path, and deploy.

### GitLab CI

```yaml
stages:
  - deploy

deploy-schema:
  stage: deploy
  tags:
    - schemasmith
  variables:
    SmithySettings_SchemaPackagePath: $CI_PROJECT_DIR
    SmithySettings_Target__Server: $DB_SERVER
    SmithySettings_Target__User: $DB_USER
    SmithySettings_Target__Password: $DB_PASSWORD
  script:
    - schemaquench
  only:
    - main
  environment:
    name: production
```

Credentials are stored as CI/CD Variables in the project settings with the masked flag enabled — GitLab redacts them from job logs automatically. The `tags` field routes the job to a runner where SchemaQuench is installed.

### Azure DevOps

```yaml
trigger:
  - main

pool:
  name: 'SchemaSmith'

steps:
  - checkout: self

  - script: schemaquench
    displayName: 'Deploy Schema'
    env:
      SmithySettings_SchemaPackagePath: $(Build.SourcesDirectory)
      SmithySettings_Target__Server: $(DB_SERVER)
      SmithySettings_Target__User: $(DB_USER)
      SmithySettings_Target__Password: $(DB_PASSWORD)
```

Credentials are stored in Variable Groups or Azure Key Vault and linked to the pipeline. The named agent pool `SchemaSmith` ensures the job runs on an agent with SchemaQuench installed.

### Runner and agent requirements

All four examples assume SchemaQuench is pre-installed on the runner or agent. SchemaSmith tools are self-contained executables with no dependencies — copy the binary to the runner, add it to the PATH, and every pipeline on that runner can use it. No package restore, no SDK installation, no version management in the pipeline itself.

## The WhatIf-in-PR pattern

This is the most powerful CI pattern you can build with SchemaSmith. It catches deployment failures before code reaches your main branch — not after.

**The idea:** run SchemaQuench in WhatIf mode on every pull request that touches schema files. WhatIf performs the full deployment logic — validation scripts, token replacement, SQL generation, dependency resolution — without executing any changes. If WhatIf succeeds, the PR is deployable. If it fails, the author knows exactly what broke before anyone reviews the code.

### PR pipeline: validate the change

The PR pipeline spins up a disposable SQL Server container, deploys the base branch schema to establish the current state, then runs WhatIf against the PR branch:

```yaml
# GitHub Actions example — WhatIf validation on PRs
name: Validate Schema Change

on:
  pull_request:
    paths:
      - 'Schema/**'

jobs:
  whatif:
    runs-on: self-hosted
    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/mssql-server:2022-latest
        env:
          ACCEPT_EULA: Y
          SA_PASSWORD: YourStr0ngPassword!
        ports:
          - 1433:1433

    steps:
      - name: Checkout PR branch
        uses: actions/checkout@v4

      - name: Deploy base schema
        env:
          SmithySettings_SchemaPackagePath: ${{ github.workspace }}
          SmithySettings_Target__Server: localhost
          SmithySettings_Target__User: sa
          SmithySettings_Target__Password: YourStr0ngPassword!
        run: |
          git checkout ${{ github.event.pull_request.base.sha }}
          schemaquench

      - name: WhatIf PR changes
        env:
          SmithySettings_SchemaPackagePath: ${{ github.workspace }}
          SmithySettings_Target__Server: localhost
          SmithySettings_Target__User: sa
          SmithySettings_Target__Password: YourStr0ngPassword!
          SmithySettings_WhatIfONLY: "true"
        run: |
          git checkout ${{ github.sha }}
          schemaquench
```

If WhatIf fails, the PR check fails. The author sees exactly which SQL statement would have broken, which token was missing, which dependency couldn't be resolved. Fix it in the PR, not in production.

### Merge pipeline: deploy through environments

Once the PR merges, the deployment pipeline takes over. A typical flow:

1. **Deploy to staging** — full SchemaQuench run against the staging database
2. **Run integration tests** — your application's test suite validates the schema change
3. **Approval gate** — manual approval before production (most CI platforms support this natively)
4. **Deploy to production** — same artifact, same SchemaQuench command, different target via environment variables

The combination catches problems at every stage. WhatIf catches SQL errors, missing tokens, and broken references in the PR. Staging deployment catches environment-specific issues. Integration tests catch application-level regressions. The approval gate gives humans the final word.

## Secret management

Every CI/CD platform has a built-in secret store. SchemaSmith's environment variable model was designed to work with all of them — every sensitive setting injected at runtime, nothing stored in files committed to source control.

| Platform | Secret storage | How it works |
|---|---|---|
| GitHub Actions | Repository Secrets | Referenced as `${{ secrets.NAME }}` in workflow env blocks |
| Jenkins | Credentials Plugin | Bound to environment variables via `credentials('id')` |
| GitLab CI | CI/CD Variables (masked) | Referenced as `$NAME` in job variables, masked in logs |
| Azure DevOps | Variable Groups / Key Vault | Referenced as `$(NAME)` in pipeline env blocks |

The pattern is the same regardless of platform: store the credential in the platform's secret store, reference it in the pipeline definition, and SchemaSmith picks it up as an environment variable. No custom integration, no plugins, no secret management SDKs. The tools read environment variables — your platform manages the secrets.

## Best practices

**Test in dev first.** Deploy to a development environment restored from a production backup before promoting anywhere. This catches edge cases that only appear with real data volumes and real object counts.

**Separate config per environment with env vars.** The same schema package deploys everywhere. Environment variables differentiate targets — server, credentials, script tokens. No environment-specific config files to maintain, no risk of deploying the wrong config to the wrong server.

**Validate before deploying.** Wire up JSON Schema validation on every PR to catch structural problems without a database. See [Testing and Validation](06-testing-and-validation.md#schema-validation-in-ci) for the exact workflow.

**WhatIf before production.** Non-negotiable. Run WhatIf against a disposable database on every PR that touches schema files. It costs minutes in CI and saves hours of incident response.

**Build once, deploy the same artifact.** Zip your schema package, version it, store it. Deploy that same zip to dev, staging, and production. If you rebuild per environment, you're not testing what you're deploying.

**Keep SchemaQuench on the runner, not in the pipeline.** Pre-install the binary on your self-hosted runners or agents. This keeps pipeline definitions clean and avoids downloading tools on every run.

---

Your pipeline's set — schema changes deploy automatically, validated at every stage. One artifact. Every environment. No manual steps. But what happens when you need to go backwards? The next chapter covers rollback and recovery. [Rollback and Recovery](08-rollback-and-recovery.md)
