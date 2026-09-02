// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain
{
    /// <summary>
    /// How SchemaTongs sequences a table's object lists when it has nothing to preserve — a first
    /// extraction, and any entry that did not exist in the file being replaced.
    /// <para>
    /// This is an extraction preference and nothing on the deploy path reads it. Making an already-deployed
    /// table's physical column order match the package is a table rebuild, not an ordering preference, and
    /// is a separate opt-in.
    /// </para>
    /// <para>
    /// Scope differs by layer, which is worth knowing before reading the SQL. This setting orders
    /// <c>Columns</c>, <c>Indexes</c>, <c>ForeignKeys</c>, <c>CheckConstraints</c> and, on SQL Server,
    /// <c>Statistics</c> and <c>XmlIndexes</c>. The stored-procedure parameter that carries it
    /// (<c>@p_ObjectOrder</c>, or <c>@SchemaSmith_ObjectOrder</c> on MySQL/MariaDB) sorts
    /// <b>columns only</b> — the remaining lists are sequenced here, after extraction returns.
    /// </para>
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ObjectOrder
    {
        /// <summary>Alphabetical by name. The default, and stable when a source table's ordinal order changes.</summary>
        Name,

        /// <summary>
        /// The table's own column order. Useful when the package is meant to read like the table does;
        /// note that two databases can order the same logical table differently, so a package extracted
        /// this way is not guaranteed to match elsewhere.
        /// </summary>
        Physical
    }
}
