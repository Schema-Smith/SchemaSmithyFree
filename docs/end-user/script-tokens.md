# Script Tokens

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## What Tokens Are

Script tokens are placeholders in the form `{{TokenName}}` that are replaced with actual values at runtime. They appear in SQL scripts, Product.json, and Template.json files.

Token replacement is case-insensitive: `{{MainDB}}`, `{{maindb}}`, and `{{MAINDB}}` all resolve to the same value.

---

## Defining Tokens

Tokens are defined in `Product.json` under the `ScriptTokens` property:

```json
{
    "ScriptTokens": {
        "MainDB": "Production",
        "ReportDB": "Reporting",
        "ReleaseVersion": "2.1.0"
    }
}
```

---

## Automatic Tokens

SchemaQuench automatically adds the following tokens:

| Token | Value |
|---|---|
| `{{ProductName}}` | The `Name` property from `Product.json` |
| `{{TemplateName}}` | The `Name` property from `Template.json` (available during template processing) |

---

## Token Override

Token values defined in `Product.json` can be overridden via configuration sources without modifying the package:

### Settings File

```json
{
    "ScriptTokens": {
        "MainDB": "Staging",
        "ReleaseVersion": "2.1.1"
    }
}
```

### Environment Variables

```bash
export SmithySettings_ScriptTokens__MainDB="Staging"
export SmithySettings_ScriptTokens__ReleaseVersion="2.1.1"
```

Overrides only apply to tokens that already exist in `Product.json`. New tokens cannot be introduced via configuration overrides alone.

---

## Where Tokens Are Resolved

Tokens are resolved in:

- **Product.json** — `ValidationScript`, `BaselineValidationScript`, `VersionStampScript`
- **Template.json** — `DatabaseIdentificationScript`, `VersionStampScript`, `BaselineValidationScript`
- **SQL script files** — All scripts in all script folders have tokens replaced before execution

---

## Token Resolution Order

When `Product.json` defines a token and a configuration override provides a different value, the override wins:

1. `Product.json` `ScriptTokens` — base values
2. `{ToolName}.settings.json` `ScriptTokens` section — overrides matching keys
3. Environment variables — overrides matching keys

---

## Related Documentation

- [Products and Templates](products-and-templates.md)
- [SchemaQuench Configuration](schemaquench/configuration.md)
