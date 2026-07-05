// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;

namespace Schema.Delivery;

/// <summary>
/// Table contract for data delivery. Implemented by the Table class (and platform
/// subclasses). The data delivery layer never references the concrete Table type —
/// only this interface.
/// </summary>
public interface IDeliverableTable
{
    /// <summary>Table name.</summary>
    string Name { get; }

    /// <summary>
    /// Database schema. "dbo" for SQL Server, "public" for PostgreSQL, null for MySQL.
    /// Base Table returns null; platform subclasses override.
    /// </summary>
    string Schema { get; }

    /// <summary>
    /// Data delivery configurations for this table, in declared order. Empty when none configured.
    /// Each entry is independently gated by its own ShouldApplyExpression (evaluated at quench).
    /// </summary>
    IReadOnlyList<DataDelivery> DataDeliveries { get; }

    /// <summary>Columns for nullability checks during FK dependency classification.</summary>
    IReadOnlyList<IDeliverableColumn> DeliverableColumns { get; }

    /// <summary>Foreign keys for topological sort and dependency graph building.</summary>
    IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys { get; }
}
