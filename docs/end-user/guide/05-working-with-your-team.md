# Working with Your Team

Schema packages are files. That means your database changes live in the same workflow your code already uses — branches, pull requests, reviews, approvals, full history. No separate migration chains, no deployment scripts maintained by hand, no hoping that staging matches production.

This chapter is about the process that keeps your team moving fast without breaking things. You've already learned how to [define your schema](04-defining-your-schema.md). Now it's time to fortify your development process — making collaboration natural, reviews meaningful, and deployments predictable.

## Source control patterns

Schema packages work with git exactly the way your application code does. Branching, merging, pull requests, full history — nothing special required.

**Branching works naturally.** Create a feature branch, add your table, modify your procedures, commit, push. Another developer does the same on their branch. Git handles the merge.

**Merge conflicts are simpler.** Two developers adding different columns to the same table? In JSON, they're adding entries to the `Columns` array — git auto-merges cleanly in most cases, and when it does conflict, the resolution is obvious (keep both entries). In migration scripts, two developers touching the same table means two separate ALTER scripts with sequence numbers that may collide, and the reviewer has to verify both scripts compose correctly.

**History tells a real story.** Every commit describes a change to the declared state of your schema. Six months from now, `git log` shows you exactly when `LoyaltyTier` was added, who added it, and what the table looked like before and after. With migration scripts, you see that someone ran an ALTER — but reconstructing the full table state at any point requires replaying every migration in sequence.

This is where schema-as-files really shines. Your schema evolves in pull requests, with reviews, approvals, and a full history — exactly like your application code already does.

## Code review with state-based diffs

Pull requests become genuinely readable when your schema is declared as state. Here's what a PR diff looks like when someone adds a column to a table:

```diff
  "Columns": [
    ...
+   {
+     "Name": "[LoyaltyTier]",
+     "DataType": "NVARCHAR(20)",
+     "Nullable": true,
+     "Default": "'Standard'"
+   },
    ...
  ]
```

A reviewer sees immediately: "This adds a nullable LoyaltyTier column with a default of 'Standard'." No need to mentally execute an ALTER script to figure out the end state. The intent is right there.

Compare that to reviewing a migration script:

```sql
ALTER TABLE [dbo].[Customers] ADD [LoyaltyTier] NVARCHAR(20) NULL
    CONSTRAINT [DF_Customers_LoyaltyTier] DEFAULT ('Standard');
```

The migration gives you less context. You see the change but not the table it lives in. Is this column next to related columns? Are there indexes that should cover it? You have to open the full table definition separately to know. With the JSON diff, the whole table is right there.

This is the collaboration unlock. Reviewers see table design, not procedural mutation. They can evaluate whether the column belongs, whether the data type is right, whether the default makes sense — all from the diff itself. The review conversation shifts from "does this script execute correctly?" to "is this the right design?"

## Team collaboration patterns

Here's a typical workflow for a team using SchemaSmith. Notice how each person focuses on their part, and the tooling handles the rest:

1. **Developer** creates a feature branch and adds a `[LoyaltyPoints]` column to the Customers table JSON.
2. **Developer** adds a stored procedure `dbo.CalculateLoyaltyPoints.sql` in the `Procedures/` folder.
3. **Developer** runs SchemaQuench against their local database to verify the changes quench cleanly.
4. **Developer** opens a pull request. The diff shows exactly one new column and one new procedure.
5. **DBA** reviews the table structure in the PR diff — or opens the package in SchemaHammer to hammer on the full table with its indexes and foreign keys side by side.
6. **Reviewer** approves. The branch merges.
7. **CI/CD** quenches the package to staging using SchemaQuench. Same package, same command.
8. **Release manager** quenches to production using SchemaQuench in WhatIf mode first, reviews the generated SQL, then runs the real deployment.

Nobody wrote a deployment script. Nobody maintained a migration chain. Nobody worried about whether staging and production are at the same migration version. The same package deploys everywhere, and SchemaQuench computes the right delta for each target. You decide what the schema looks like; the forge makes it happen.

## WhatIf mode as your safety net

WhatIf mode is the preview button for your database. Run it before every deployment — especially production. Boring deployments are the goal, and WhatIf is how you keep them boring.

```bash
SmithySettings_WhatIfONLY=true SchemaQuench
```

SchemaQuench does everything it normally does — connects to the target database, computes the delta between the declared state and the current state, generates the SQL — but stops short of executing. The generated SQL is written to log files in the working directory so you can review every statement.

Build this into your workflow:

- **Development:** Optional. Quench directly if you're comfortable.
- **Staging:** Recommended. Review the WhatIf output to catch surprises before they hit production-like data.
- **Production:** Non-negotiable. Always WhatIf first. Read every line of generated SQL. Then quench.

The cost is one extra command. The benefit is never being surprised by what a deployment does to your production database.

For the full details on WhatIf behavior and output files, see [SchemaQuench Reference — WhatIf Mode](../reference/schemaquench.md#whatif-mode).

## Using SchemaHammer for review

SchemaHammer is the visual side of SchemaSmith. Open a product to browse its full structure — tables, columns, indexes, procedures, views — in a navigable tree. It's your workbench for inspecting and understanding the shape of your schema.

During code review, SchemaHammer adds context that a raw diff can't provide:

- **Browse the full table.** A PR diff shows the column you added. SchemaHammer shows the column in context with every other column, index, and foreign key on the table.
- **Code search across scripts.** Wondering which stored procedures reference the column you're about to rename? Use code search to find every reference across all procedures, functions, views, and triggers in the package.
- **Token preview.** Script tokens let you parameterize environment-specific values. SchemaHammer shows you what the resolved script looks like, so you can verify token substitution before deployment.

SchemaHammer isn't required for any workflow — everything works from the command line. But when you want to understand the big picture or investigate cross-cutting changes, it's the fastest path to answers.

For the full feature set, see [SchemaHammer Reference](../reference/schemahammer.md).

---

Reviews that show intent. Previews that catch surprises. A workflow that fits how you already work. Now let's fortify the process further with testing and validation. [Testing and Validation](06-testing-and-validation.md)
