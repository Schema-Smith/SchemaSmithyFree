// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Schema.Domain;

namespace Schema.Capabilities
{
    /// <summary>
    /// The frozen list of version-gated features SchemaSmith degrades below a floor, one row per feature per
    /// exact platform. The single source of truth for "does this authored feature need a newer engine, and
    /// what happens below that" — consumed as informational (never-blocking) metadata. Kept honest against the
    /// deploy-time <c>.sql</c> guards by the capability consistency tests (structural + version-boundary probes).
    ///
    /// Inclusion rule (behavior, not mechanism): a feature is listed when its declared form is actually skipped
    /// or applied with reduced fidelity below a version. A feature supported via a different mechanism with the
    /// same end-state (e.g. PostgreSQL <c>MERGE</c> emulated as <c>INSERT .. ON CONFLICT</c>, column rename via
    /// <c>CHANGE COLUMN</c>, index rename via drop+recreate, the MariaDb recursive-CTE data-delivery shred) is
    /// NOT a degrade and is deliberately absent.
    /// </summary>
    public static class CapabilityRegistry
    {
        /// <summary>All catalogued degrade capabilities, per exact platform.</summary>
        public static IReadOnlyList<Capability> All { get; } = BuildAll();

        /// <summary>The capabilities for one exact platform (MariaDb is distinct from MySQL).</summary>
        public static IReadOnlyList<Capability> For(Platform platform) =>
            All.Where(c => c.Platform == platform).ToList();

        private static List<Capability> BuildAll()
        {
            var rows = new List<Capability>();

            // ---- SQL Server (binary/major-version gated via fn_ServerMajorVersion; all skipped) -------------
            rows.Add(new("temporal", "Temporal tables (system-versioning)", Platform.SqlServer,
                13, "SQL Server 2016", null, DegradeKind.Skip, "temporal (SQL Server 2016)"));
            rows.Add(new("data-masking", "Dynamic data masking", Platform.SqlServer,
                13, "SQL Server 2016", null, DegradeKind.Skip, "data masking (SQL Server 2016)"));
            rows.Add(new("always-encrypted", "Always Encrypted", Platform.SqlServer,
                13, "SQL Server 2016", null, DegradeKind.Skip, "Always Encrypted (SQL Server 2016)"));
            // Columnstore splits by index kind: nonclustered arrived in 2012 (major 11), clustered in 2014 (12).
            // Both share the one ChangeAudit ObjectType.
            rows.Add(new("columnstore-nonclustered", "Nonclustered columnstore index", Platform.SqlServer,
                11, "SQL Server 2012", null, DegradeKind.Skip, "columnstore index (SQL Server 2012/2014)"));
            rows.Add(new("columnstore-clustered", "Clustered columnstore index", Platform.SqlServer,
                12, "SQL Server 2014", null, DegradeKind.Skip, "columnstore index (SQL Server 2012/2014)"));

            // ---- PostgreSQL (major gated via ServerVersionNum; all skipped) ---------------------------------
            rows.Add(new("nulls-not-distinct", "Unique index NULLS NOT DISTINCT", Platform.PostgreSQL,
                15, "PostgreSQL 15", null, DegradeKind.Skip, "NULLS NOT DISTINCT (PG15)"));
            rows.Add(new("expression-statistics", "Extended statistics on expressions", Platform.PostgreSQL,
                14, "PostgreSQL 14", null, DegradeKind.Skip, "expression statistics (PG14)"));
            rows.Add(new("column-compression", "Per-column compression method", Platform.PostgreSQL,
                14, "PostgreSQL 14", null, DegradeKind.Skip, "per-column compression (PG14)"));
            rows.Add(new("table-access-method", "Table access method", Platform.PostgreSQL,
                15, "PostgreSQL 15", null, DegradeKind.Skip, "table access method (PG15)"));
            // Gated inline in MissingIndexesAndConstraintsQuench rather than by a Supports*.sql function,
            // which is exactly why the completeness guard could not see it: it shipped in v2.5.0 as a real
            // policy-routed degrade with no row here, invisible to the add-ons that drive off this list.
            rows.Add(new("virtual-generated-column", "VIRTUAL generated columns", Platform.PostgreSQL,
                18, "PostgreSQL 18", null, DegradeKind.Skip, "VIRTUAL generated column (PG18)"));

            // ---- MySQL (comparable major*100+minor via SchemaSmith_ServerVersionNum) ------------------------
            rows.Add(new("invisible-index", "Invisible index", Platform.MySQL,
                800, "MySQL 8.0", null, DegradeKind.Skip, "INDEX (invisible, MySQL 8.0 / MariaDB 10.6)"));
            rows.Add(new("descending-index", "Descending index key part", Platform.MySQL,
                800, "MySQL 8.0", null, DegradeKind.Reduced, "INDEX (descending key part, MySQL 8.0 / MariaDB 10.8)"));
            // CHECK's true intro is 8.0.16; the gate is major-only (800). Comparable matches the gate, display
            // matches the manifest message (which names the 8.0.16 patch).
            rows.Add(new("check-constraint", "CHECK constraint", Platform.MySQL,
                800, "MySQL 8.0.16", null, DegradeKind.Skip, "CHECK constraint (MySQL 8.0.16)"));
            // Automatic data delivery needs JSON_TABLE (MySQL 8.0). MySQL 5.7 has neither JSON_TABLE nor
            // recursive CTEs, so there is no row-source fallback: the delivery is skipped (warn) or aborts
            // (fail) at the C# layer (DataDeliveryProcessor) — a genuine skip, but with no ChangeAudit manifest
            // row, so ManifestObjectType is null. (MariaDb is absent: 10.2-10.5 deliver the same rows via a
            // recursive-CTE shred — equivalent-path, not a degrade.)
            rows.Add(new("data-delivery", "Automatic data delivery (JSON_TABLE row source)", Platform.MySQL,
                800, "MySQL 8.0", null, DegradeKind.Skip, null));

            // Column DEFAULT expression: below the floor the whole column is skipped (not just the clause —
            // see MissingTableAndColumnQuench/ModifiedTableQuench). MariaDB is absent on purpose: MDEV-10134
            // landed in 10.2.1, at/below our 10.2 floor, so SchemaSmith_SupportsDefaultExpression() is
            // unconditionally 1 on MariaDB — same "no row, no version boundary to cross" shape as the
            // MySQL-only check-constraint row above.
            rows.Add(new("default-expression", "Column DEFAULT expression", Platform.MySQL,
                800, "MySQL 8.0.13", null, DegradeKind.Skip, "column (DEFAULT expression, MySQL 8.0.13)"));

            // Invisible column: unlike invisible-index (INVISIBLE vs. IGNORED keyword differs by engine), the
            // INVISIBLE keyword itself is identical on both engines — only the introduction version differs,
            // so both platforms get a row with a shared ManifestObjectType literal (mirrors invisible-index).
            // MySQL row here; the MariaDb row lives in the MariaDb section below, alongside its siblings.
            rows.Add(new("invisible-column", "Invisible column", Platform.MySQL,
                800, "MySQL 8.0.23", null, DegradeKind.Reduced, "column (invisible, MySQL 8.0.23 / MariaDB 10.3)"));

            // Column SRID restriction: MariaDB has NO equivalent at any version (verified live on 11.4 — the
            // syntax is a hard parse error and INFORMATION_SCHEMA.COLUMNS carries no SRS_ID at all), so
            // SchemaSmith_SupportsColumnSrid() returns 0 unconditionally on MariaDB — there is no version
            // boundary for a MariaDB row to encode (IntroducedInComparable documents "the literal the .sql
            // guard compares against"; MariaDB's guard compares against nothing, it just always returns 0).
            // Same "no row" treatment as the MySQL-only rows above, just unconditionally-0 instead of
            // unconditionally-1 — the registry has no way to say "never" other than omission, so MariaDB gets
            // no row for this key, same as it gets no row for check-constraint (there, unconditionally 1).
            rows.Add(new("column-srid", "Column SRID restriction", Platform.MySQL,
                800, "MySQL 8.0.3", null, DegradeKind.Reduced, "column (SRID, MySQL 8.0.3)"));

            // Functional/expression index (including a multi-valued index, CAST(... AS ... ARRAY) —
            // MySQL 8.0.17+, same NULL-COLUMN_NAME/EXPRESSION shape as a plain functional key part, so it
            // rides this same row rather than needing one of its own): below the floor the whole index is
            // skipped, same "no reduced form" shape as default-expression above (a functional key part is
            // a hard syntax error, not a clause that can be dropped and still leave a valid index). MariaDB
            // is absent on purpose: it has no equivalent in this form at ANY version, so
            // SchemaSmith_SupportsFunctionalIndex() is unconditionally 0 there — same "no row, no version
            // boundary to cross" shape as column-srid above, just unconditionally-0 instead of
            // unconditionally-1 (default-expression's MariaDB shape).
            rows.Add(new("functional-index", "Functional/expression index", Platform.MySQL,
                800, "MySQL 8.0.13", null, DegradeKind.Skip, "INDEX (functional/expression, MySQL 8.0.13)"));

            // ---- MariaDb (distinct rows — different intro versions; NO CHECK row: supported at the 10.2 floor)
            rows.Add(new("invisible-index", "Invisible index (IGNORED)", Platform.MariaDb,
                1006, "MariaDB 10.6", null, DegradeKind.Skip, "INDEX (invisible, MySQL 8.0 / MariaDB 10.6)"));
            rows.Add(new("descending-index", "Descending index key part", Platform.MariaDb,
                1008, "MariaDB 10.8", null, DegradeKind.Reduced, "INDEX (descending key part, MySQL 8.0 / MariaDB 10.8)"));
            // Real threshold on this engine too (unlike column-srid/default-expression, which are never/always
            // supported on MariaDb) — see the MySQL row above for the shared-literal rationale.
            rows.Add(new("invisible-column", "Invisible column", Platform.MariaDb,
                1003, "MariaDB 10.3", null, DegradeKind.Reduced, "column (invisible, MySQL 8.0.23 / MariaDB 10.3)"));

            return rows;
        }
    }
}
