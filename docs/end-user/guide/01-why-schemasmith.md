# Why SchemaSmith

You know the feeling. It's Thursday afternoon and someone needs a column added to a production table. So you write an ALTER script, test it against a dev copy that's three weeks stale, cross your fingers, and run it in production. It works — this time. Next month, a different script fails because someone else already renamed that index and nobody updated the migration folder. Now you're debugging deployment archaeology at 10pm.

Hand-written migration scripts are the status quo, and the status quo is fragile. Every ALTER is a bet that you know exactly what the target database looks like right now. Migration folders grow into long, ordered chains where one bad link breaks everything downstream. DBAs spend their review cycles reading procedural diffs — "add this column, drop that index, rename this constraint" — instead of reviewing the actual table design. And when something drifts, the answer to "who changed this column and when?" lives in a ticket somewhere, maybe.

Deployment fear slows the whole team down. Developers wait for DBA approval. DBAs wait for confidence that the script matches reality. Everyone waits because the cost of getting it wrong is a production outage.

There is a better model, and you already use it for everything else.

## Declare the state, not the steps

You don't write diffs of your C# classes and apply them one by one. You declare the class and the compiler figures out the rest. Your infrastructure team doesn't hand-write sequential cloud change scripts — they declare the desired state in Terraform and let the tool compute the delta.

SchemaSmith brings that same model to SQL Server databases. You declare what each table, view, procedure, and trigger should look like. The tool compares your declaration against the live database, computes what changed, and generates the correct ALTER and CREATE scripts. You review structure, not migration steps. The database converges to match your declaration every time, on every target.

## Four tools, one lifecycle

SchemaSmith is a toolset of four components that cover the full schema lifecycle:

**[SchemaTongs](../reference/schematongs.md)** extracts your existing database into a clean, organized package — tables as JSON, programmable objects as SQL files, everything structured for humans to read and source control to track.

**[SchemaHammer](../reference/schemahammer.md)** is a desktop viewer that lets you browse and review schema packages visually. Open a package, navigate the tree, inspect table definitions with syntax highlighting. It turns a folder of files into something a DBA can review in minutes.

**[SchemaQuench](../reference/schemaquench.md)** deploys a schema package to any SQL Server. It reads your declared state, compares it to the target database, and applies only the changes needed. No migration ordering. No manual diffing. Run it against dev, staging, and production — same package, correct results everywhere.

**[DataTongs](../reference/datatongs.md)** captures reference data — lookup tables, configuration rows, seed data — as deployable MERGE scripts. Extract once, deploy alongside your schema.

## How teams actually use this

A developer needs to add a column to the Orders table. They open `Orders.json`, add the column definition, and submit a pull request. The DBA opens the PR — or loads the package in SchemaHammer — and reviews the table structure directly. Not "ALTER TABLE Orders ADD..." but the full table definition, clear and complete.

Nobody writes ALTER scripts. Nobody maintains migration ordering. Nobody worries about whether the target database matches the assumptions baked into a migration chain. Source control tracks what the table looks like over time, the same way it tracks application code.

The developer thinks in terms of table design. The DBA reviews table design. The tool handles the translation to deployment scripts. Everyone works at the right level of abstraction.

## What you get

Self-contained executables — download, extract, run. No .NET runtime to install, no dependency chains to manage. Available for Windows, macOS, and Linux on both x64 and ARM64.

Licensed under the SchemaSmith Community License (SSCL v2.0): use it for any purpose, in any size organization, at any revenue level, with no restrictions on database size or environment count. Free means free.

Production-tested against real-world schemas. The demo includes AdventureWorks (71 tables) and Northwind (13 tables) so you can see it work before pointing it at your own databases.

---

Ready to see it in action? [Get started with your first database →](02-quick-start.md)
