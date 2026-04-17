// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Delivery;

/// <summary>
/// Minimal foreign key contract for data delivery FK dependency classification.
/// A topological sort is built from these edges. Implemented by the platform-specific
/// ForeignKey subclasses (SqlServerForeignKey, PostgreSqlForeignKey, MySqlForeignKey).
/// </summary>
public interface IDeliverableForeignKey
{
    /// <summary>CSV column names in the FK constraint.</summary>
    string Columns { get; }

    /// <summary>Target table name of the FK relationship.</summary>
    string RelatedTable { get; }

    /// <summary>
    /// Target table schema. "dbo" for SQL Server, "public" for PostgreSQL,
    /// null or empty for MySQL (database-level scoping).
    /// </summary>
    string RelatedTableSchema { get; }
}
