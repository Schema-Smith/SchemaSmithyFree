# Script Tokens

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## What Tokens Are

Script tokens are placeholders in the form `{{TokenName}}` that are replaced with actual values at runtime. They appear in SQL scripts, Product.json, and Template.json files.

Token replacement is case-insensitive: `{{MainDB}}`, `{{maindb}}`, and `{{MAINDB}}` all resolve to the same value.

---

## Defining Tokens

### Product-Level Tokens

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

### Template-Level Tokens

Templates can also define their own `ScriptTokens` in `Template.json`. Template tokens override product tokens with the same key for the duration of that template's execution:

```json
{
    "Name": "Reporting",
    "DatabaseIdentificationScript": "SELECT [name] FROM master.sys.databases WHERE [name] = '{{ReportDB}}'",
    "ScriptTokens": {
        "MainDB": "ReportingAlias",
        "SchemaOwner": "rpt"
    }
}
```

In this example, `{{MainDB}}` resolves to `"ReportingAlias"` within the Reporting template, overriding the product-level value of `"Production"`. Token keys defined only in the template (such as `{{SchemaOwner}}`) are available within that template but not in other templates or product-level scripts.

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

Configuration and environment overrides are applied at the product level, before template-level tokens are merged in.

---

## Where Tokens Are Resolved

Tokens are resolved in:

- **Product.json** — `ValidationScript`, `BaselineValidationScript`, `VersionStampScript`
- **Template.json** — `DatabaseIdentificationScript`, `VersionStampScript`, `BaselineValidationScript`
- **SQL script files** — All `.sql` files in all script folders (Before, Objects, AfterTablesObjects, BetweenTablesAndKeys, AfterTablesScripts, Table Data, After, and product Before/After) have tokens replaced before execution

---

## Token Resolution Order

Tokens are resolved in layers, with later layers overriding earlier ones:

1. `Product.json` `ScriptTokens` — base values
2. `{ToolName}.settings.json` `ScriptTokens` section — overrides matching keys
3. Environment variables — overrides matching keys
4. `Template.json` `ScriptTokens` — overrides matching keys for the duration of that template's execution only

Configuration and environment overrides (steps 2–3) apply at the product level. Template tokens (step 4) are then merged on top of the resolved product token set before each template runs, and do not affect other templates or product-level scripts.

---

## Related Documentation

- [Products and Templates](products-and-templates.md)
- [SchemaQuench Configuration](schemaquench/configuration.md)
