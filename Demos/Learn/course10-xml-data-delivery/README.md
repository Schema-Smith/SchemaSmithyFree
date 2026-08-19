# Course 10 bonus recipe — XML data delivery for the legacy tier

Course 10's oldest SQL Server tier (`learn_2008`, compatibility level 100) is where
SchemaSmith's own model ingest quietly switches from JSON to XML — you never see it, because
it's the *engine's* choice (see [Module 4](../course10-module-04/README.md)). This recipe is
about the one knob that puts the same choice in **your** hands: `DataDelivery.ContentEncoding`.

There's only **one product** here, `RefData`, and its `dbo.CountryCode` table. You'll deploy it
twice: first as it's naturally authored — a default JSON `DataDelivery` — and watch that delivery
hit a wall on the legacy tier; then with one property changed, `"ContentEncoding": "Xml"`, and
watch the identical data land everywhere. `sqlserver/json-attempt/` and `sqlserver/package/` are
NOT two products — they're the *same* `RefData` product, before and after that one-line fix, the
same edit-and-redeploy idiom Module 3 used for gating.

A `DataDelivery` block defaults to JSON, shredded with `OPENJSON` — which needs SQL Server
compatibility level 130+. Below that, `OPENJSON` doesn't degrade gracefully; it parse-errors.
Set `"ContentEncoding": "Xml"` on the delivery and SchemaSmith shreds the payload with the XML
data-type methods (`.nodes()`/`.value()`) instead — a path that works at *every* compatibility
level, including the oldest tier in the fleet.

## Prerequisites

- The four-engine sandbox is up (`Demos/Learn/docker`).
- **Run [`course10-setup`](../course10-setup/README.md) first** so the mixed fleet is standing:
  `learn_2022` (compat 160) and `learn_2008` (compat 100) on the shared SQL Server instance
  (`localhost,11433`), and current-tier PostgreSQL 16 (`localhost:15432`).
- `schemaquench --version` answers **2.4.0** — this recipe pins the released CLI, no
  from-source override.

## Part 1 — the problem: JSON delivery hits the compat-130 cliff

`sqlserver/json-attempt` is the `RefData` product as you'd naturally author it: `dbo.CountryCode`
with a plain JSON `DataDelivery` — no `ContentEncoding`, so it defaults to `Json` — pointed at a
JSON `.tabledata` array. Deploy it to `learn_2008`:

```
cd sqlserver
schemaquench --ConfigFile:quench.settings.json-attempt.json --LogPath:"$PWD/logs"
```

The table itself deploys fine — only the *data delivery* hits the cliff. Under the default
`warn` policy, SchemaQuench skips just that delivery with a clear message and leaves the table
empty, rather than failing the whole run:

```
[localhost,11433].[learn_2008]   detected SQL Server version 16.0.4260.1 (compatibility level 100)
[localhost,11433].[learn_2008]   Delivering table data
[localhost,11433].[learn_2008]     [SKIPPED - requires compatibility level 130 for JSON delivery] JSON data delivery for dbo.CountryCode requires SQL Server compatibility level 130 (target is at 100); re-encode this delivery as XML ("ContentEncoding": "Xml") to deploy it on a legacy-compat target.
[localhost,11433].[learn_2008] Successfully Quenched
```

Exit code `0` — `warn` skips and the run still succeeds. Row count after: `0` — the table was
created, but the delivery that would have populated it never ran.

Now flip the policy. `quench.settings.json-attempt-fail.json` is identical except it sets
`"UnsupportedFeaturePolicy": "fail"` under `Target`. Same package, same target, same JSON
delivery — this time SchemaQuench aborts the run instead of skipping:

```
schemaquench --ConfigFile:quench.settings.json-attempt-fail.json --LogPath:"$PWD/logs"
```

```
[localhost,11433].[learn_2008] FAILED to quench:
JSON data delivery for dbo.CountryCode requires SQL Server compatibility level 130 (target is at 100); re-encode this delivery as XML ("ContentEncoding": "Xml") to deploy it on a legacy-compat target.
[localhost,11433].[learn_2008] *** FAILED [Template:Main] ***
Template 'Main' had 1 failed work unit(s)
```

Exit code `2`.

Both outcomes are the built-in degrade behavior for a JSON-encoded delivery aimed at a
below-130 target — `warn` (the default) skips the delivery and lets the rest of the deploy
proceed; `fail` aborts before any deployment work begins. Neither one lands the reference data
on the legacy tier. That's the fix this recipe teaches.

## Part 2 — the fix: `ContentEncoding: "Xml"`

`sqlserver/package` is the *same* `RefData` product, redeployed with one change: `dbo.CountryCode`
is unchanged, but its `DataDelivery` is:

```json
"DataDelivery": {
  "ContentFile": "data/dbo.CountryCode.xml.tabledata",
  "ContentEncoding": "Xml",
  "MergeType": "Insert/Update",
  "MatchColumns": "Code"
}
```

pointed at `data/dbo.CountryCode.xml.tabledata`, which carries the same five rows in the
documented XML shape — one `<c>` element per column, named by an `n` attribute:

```xml
<rows>
<row><c n="Code">US</c><c n="Name">United States</c><c n="Continent">North America</c></row>
<row><c n="Code">CA</c><c n="Name">Canada</c><c n="Continent">North America</c></row>
<row><c n="Code">GB</c><c n="Name">United Kingdom</c><c n="Continent">Europe</c></row>
<row><c n="Code">DE</c><c n="Name">Germany</c><c n="Continent">Europe</c></row>
<row><c n="Code">JP</c><c n="Name">Japan</c></row>
</rows>
```

Notice the last row: `JP` has no `<c n="Continent">` element at all. **An absent `<c>` is
`NULL`** — not an empty string, not a parse error. That's the documented contract for the XML
encoding, and it's the row this recipe uses to prove it.

Deploy the XML package to `learn_2008`:

```
schemaquench --ConfigFile:quench.settings.legacy.json --LogPath:"$PWD/logs"
```

This time the delivery isn't gated on anything — `.nodes()`/`.value()` shredding works at
compatibility level 100, so all five rows land, including `JP` with a `NULL` continent:

```
[localhost,11433].[learn_2008]   Delivering table data
[localhost,11433].[learn_2008]     Delivering dbo.CountryCode
[localhost,11433].[learn_2008] Successfully Quenched
```

Exit code `0`. Read the rows back and confirm the `NULL`:

```
sqlcmd -S localhost,11433 -U sa -P "Learn!Passw0rd" -C -d learn_2008 -Q "SELECT Code, Name, Continent FROM dbo.CountryCode ORDER BY Code;"
```

```
Code  Name            Continent
----  --------------  -------------
CA    Canada          North America
DE    Germany         Europe
GB    United Kingdom  Europe
JP    Japan           NULL
US    United States   North America
```

`JP.Continent` is `NULL` — the absent-`<c>` contract, holding on a tier `OPENJSON` can't even
parse. That's the money shot: the same reference data your JSON attempt couldn't get past the
compat-130 cliff now lands cleanly, on the oldest tier in the fleet.

## Part 3 — portable: the same XML package on the modern tier

`ContentEncoding: "Xml"` isn't a legacy-only mode — it's just a different shred path, and it
works everywhere SQL Server does. Deploy the *same* `sqlserver/package` to `learn_2022`
(compat 160):

```
schemaquench --ConfigFile:quench.settings.modern.json --LogPath:"$PWD/logs"
```

```
[localhost,11433].[learn_2022]   Delivering table data
[localhost,11433].[learn_2022]     Delivering dbo.CountryCode
[localhost,11433].[learn_2022] Successfully Quenched
```

```
sqlcmd -S localhost,11433 -U sa -P "Learn!Passw0rd" -C -d learn_2022 -Q "SELECT Code, Name, Continent FROM dbo.CountryCode ORDER BY Code;"
```

```
Code  Name            Continent
----  --------------  -------------
CA    Canada          North America
DE    Germany         Europe
GB    United Kingdom  Europe
JP    Japan           NULL
US    United States   North America
```

Row-for-row identical to the `learn_2008` readback. One package, one `DataDelivery` block, both
tiers — you don't maintain a JSON copy for modern targets and an XML copy for the old one.

## Part 4 — parity: XML is SQL-Server-only

PostgreSQL, MySQL, and MariaDB shred their delivery data at *every* supported version — they
have no `OPENJSON`-style compat cliff, so they have no encoding choice to make. Declaring
`ContentEncoding: "Xml"` on those platforms is rejected, not silently ignored.

`postgres/package` declares `public.countrycode` with the same `ContentEncoding: "Xml"` block,
lowercase-named for PostgreSQL. Deploy it:

```
cd ../postgres
schemaquench --ConfigFile:quench.settings.json --LogPath:"$PWD/logs"
```

```
[localhost].[learn]         Create new table public.countrycode
[localhost].[learn]         Add missing Constraint public.countrycode.pk_countrycode
[localhost].[learn]   Delivering table data
[localhost].[learn]     Error delivering public.countrycode: XML data-delivery encoding is not yet supported on PostgreSQL; use JSON (its shred works at every supported version).
[localhost].[learn] FAILED to quench:
Data delivery failed for 1 table(s): public.countrycode
[localhost].[learn] *** FAILED [Template:Main] ***
```

Exit code `2`. The table itself is plain DDL and deploys fine — it lands with `0` rows; it's the
data-delivery step that rejects the `Xml` encoding, throwing an exception that names the platform
and points you at JSON — which fails the run rather than silently falling back to a JSON shred of
an XML payload:

```
XML data-delivery encoding is not yet supported on PostgreSQL; use JSON (its shred works at every supported version).
```

That's not a parity gap to close later — it's an accurate reflection of where the cliff
actually exists. SQL Server is the only engine whose oldest supported tier can't parse JSON at
all, so it's the only one that needs a second encoding. PostgreSQL has had JSON functions since
9.2; MySQL and MariaDB fall back to `JSON_EXTRACT` below their JSON-function floor — still
JSON, every time. Use `Json` (the default — just omit `ContentEncoding`) for `DataDelivery` on
PostgreSQL, MySQL, and MariaDB.

## Cleanup

```
cd ../sqlserver
sqlcmd -S localhost,11433 -U sa -P "Learn!Passw0rd" -C -d learn_2022 -Q "DROP TABLE IF EXISTS dbo.CountryCode;"
sqlcmd -S localhost,11433 -U sa -P "Learn!Passw0rd" -C -d learn_2008 -Q "DROP TABLE IF EXISTS dbo.CountryCode;"

cd ../postgres
PGPASSWORD="Learn!Passw0rd" psql -h localhost -p 15432 -U postgres -d learn -c "DROP TABLE IF EXISTS public.countrycode;"
```

## What's in here

| Path | Purpose |
| --- | --- |
| `sqlserver/package/` | Product `RefData`, the fix — `dbo.CountryCode` with `ContentEncoding: "Xml"`, deployed to both `learn_2008` and `learn_2022`. |
| `sqlserver/json-attempt/` | The *same* product `RefData`, the problem — `dbo.CountryCode` with a default-JSON `DataDelivery`, which hits the compat-130 cliff on `learn_2008`. Not a second product; SchemaSmith's ownership guard would reject two products claiming the same table. |
| `sqlserver/quench.settings.legacy.json` / `…modern.json` | The XML-encoded `RefData` deployed to `learn_2008` (compat 100) and `learn_2022` (compat 160). |
| `sqlserver/quench.settings.json-attempt.json` / `…json-attempt-fail.json` | The JSON-encoded `RefData` attempt under the default `warn` policy and under `Target:UnsupportedFeaturePolicy=fail`. |
| `postgres/package/` | An XML-delivery package on `Platform: PostgreSQL`, used only to demonstrate the rejection. |

Editor JSON-schema files (`.json-schemas/`) for all three packages are generated by the CLI's
`--WriteSchemasOnly` switch and are not hand-authored.

## Up next

Back to [Module 4](../course10-module-04/README.md) if you haven't read it yet — it's the
companion story: the engine's own model ingest makes this same JSON-vs-XML choice
*automatically*, with no flag, no package change. This recipe is the one place SchemaSmith
leaves that choice to you, and only for the payload that's your data, not the engine's model.
