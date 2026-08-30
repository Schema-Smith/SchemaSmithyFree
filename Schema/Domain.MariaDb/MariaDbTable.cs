// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain.MySQL;

namespace Schema.Domain.MariaDb
{
    /// <summary>
    /// A MariaDB table. Everything MariaDB shares with MySQL — which is nearly all of it — comes from
    /// <see cref="MySqlTable"/>; this type exists to carry what MariaDB has and MySQL does not.
    /// <para>
    /// Before it existed, <c>Platform.MariaDb</c> deserialized to <see cref="MySqlTable"/> and there was
    /// no way to scope a property to one of the two engines: a MariaDB-only property added to the shared
    /// type would appear in MySQL's generated <c>tables.mysql.schema</c> as well, offering MySQL users a
    /// setting the engine cannot honour and an editor that green-lights it.
    /// </para>
    /// </summary>
    public class MariaDbTable : MySqlTable
    {
        /// <summary>
        /// Whether the table keeps its own row history (MariaDB <c>WITH SYSTEM VERSIONING</c>, 10.3+).
        /// <para>
        /// Detected from <c>INFORMATION_SCHEMA.TABLES.TABLE_TYPE = 'SYSTEM VERSIONED'</c>, which answers
        /// for BOTH authoring forms — the implicit form exposes nothing else at all, so anything built on
        /// period columns would see such a table as plain.
        /// </para>
        /// <para>
        /// MariaDB-only by nature: MySQL has no system versioning at any version. It lives here rather
        /// than on <c>MySqlTable</c> so it cannot appear in <c>tables.mysql.schema</c> and offer MySQL
        /// users a setting the engine cannot honour.
        /// </para>
        /// </summary>
        [JsonProperty(Order = 110)]
        public bool IsSystemVersioned { get; set; }

    }
}
