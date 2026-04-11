// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaSmith.Pro;

namespace Schema.Domain.PostgreSQL;

/// <summary>
/// PostgreSQL-specific data delivery configuration. MergeDisableRules and
/// MergeUpdateDescendents are now defined on the base DataDelivery class.
/// This subclass is retained for backward compatibility with existing JSON deserialization.
/// </summary>
public class PostgreSqlDataDelivery : DataDelivery
{
}
