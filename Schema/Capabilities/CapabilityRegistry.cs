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

            // ---- MariaDb (distinct rows — different intro versions; NO CHECK row: supported at the 10.2 floor)
            rows.Add(new("invisible-index", "Invisible index (IGNORED)", Platform.MariaDb,
                1006, "MariaDB 10.6", null, DegradeKind.Skip, "INDEX (invisible, MySQL 8.0 / MariaDB 10.6)"));
            rows.Add(new("descending-index", "Descending index key part", Platform.MariaDb,
                1008, "MariaDB 10.8", null, DegradeKind.Reduced, "INDEX (descending key part, MySQL 8.0 / MariaDB 10.8)"));

            return rows;
        }
    }
}
