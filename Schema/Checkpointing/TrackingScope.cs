// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Checkpointing;

/// <summary>
/// Identifies the deployment context for checkpoint tracking. Product-level tracking
/// sets only ProductName; database-level tracking sets all four classic fields; per-iteration
/// (schema-template) tracking additionally sets SchemaName. The fields are used to construct a
/// unique key for state storage at both layers — in-memory FileCheckpointManager scope keys
/// and the persisted SchemaSmith.CompletedMigrationScripts table's composite key.
/// </summary>
public class TrackingScope
{
    public string ProductName { get; set; }
    public string TemplateName { get; set; }
    public string Server { get; set; }
    public string DatabaseName { get; set; }

    /// <summary>
    /// Per-iteration schema name for schema templates. Empty string for regular templates
    /// (the schema-template feature is intentionally SQL-Server / PostgreSQL only — MySQL
    /// scopes always have this empty). Strict equality required when looking up tracking
    /// rows: a legacy blank-schema row must not shadow a per-tenant lookup.
    /// </summary>
    public string SchemaName { get; set; } = "";

    /// <summary>
    /// Pipe-joined composite of all five dimensions. Stable order:
    /// ProductName|TemplateName|Server|DatabaseName|SchemaName. Used as the
    /// FileCheckpointManager dictionary key so two iterations of the same template against
    /// the same DB but for different tenant schemas don't collide.
    /// </summary>
    public string CompositeKey =>
        $"{ProductName}|{TemplateName}|{Server}|{DatabaseName}|{SchemaName}";

    /// <summary>
    /// Server/database identity for progress and error logs. Distinct from CompositeKey, which is a
    /// storage key and must stay stable; this is the human-facing rendering the log interpolations want.
    /// </summary>
    public override string ToString() => $"[{Server}].[{DatabaseName}]";
}
