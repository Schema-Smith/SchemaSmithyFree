// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain.MySQL;

namespace Schema.Domain.MariaDb
{
    /// <summary>
    /// A MariaDB table. Everything MariaDB shares with MySQL — which is nearly all of it — comes from
    /// <see cref="MySqlTable"/>; this type exists to carry what MariaDB has and MySQL does not.
    /// <para>
    /// It is deliberately empty at first. Before it existed, <c>Platform.MariaDb</c> deserialized to
    /// <see cref="MySqlTable"/> and there was no way to scope a property to one of the two engines: a
    /// MariaDB-only property added to the shared type would appear in MySQL's generated
    /// <c>tables.mysql.schema</c> as well, offering MySQL users a setting the engine cannot honour and
    /// an editor that green-lights it. The seam is the point; the content arrives with the first
    /// MariaDB-only feature (system-versioned tables).
    /// </para>
    /// <para>
    /// Adding no properties means the generated <c>tables.mariadb.schema</c> is byte-identical to what
    /// the shared type produced, so introducing this type churns no committed artifact on its own.
    /// </para>
    /// </summary>
    public class MariaDbTable : MySqlTable
    {
    }
}
