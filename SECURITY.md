# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.1.x   | Yes       |
| < 1.1   | No        |

## Reporting a Vulnerability

**Do not open a public issue for security vulnerabilities.**

Instead, please report security issues by emailing **security@schemasmith.com** with:

- Description of the vulnerability
- Steps to reproduce
- Affected tool(s) and version(s)
- Impact assessment if known

You should receive an acknowledgment within 48 hours. We will work with you to understand the issue and coordinate a fix before any public disclosure.

## Scope

SchemaSmith tools connect to SQL Server databases and execute SQL. Security-relevant areas include:

- **Connection credential handling** — passwords are redacted in logs, but configuration files may contain credentials
- **SQL generation** — tools generate and execute dynamic SQL against target databases
- **File handling** — tools read schema packages from disk or ZIP archives

## Best Practices

- Use Windows integrated authentication when possible instead of SQL credentials in config files
- Restrict file system permissions on configuration files containing credentials
- Use environment variables (`SmithySettings_` prefix) for sensitive settings in CI/CD pipelines
- Review WhatIf output before applying changes to production databases
