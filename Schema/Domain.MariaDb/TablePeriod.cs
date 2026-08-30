// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.MariaDb
{
    /// <summary>
    /// A MariaDB application-time period: a named pair of columns that together describe the interval a
    /// row is valid for (<c>PERIOD FOR validity(start_date, end_date)</c>, 10.4.3+).
    /// <para>
    /// Distinct from system versioning, which records when a row was <i>stored</i>. An application-time
    /// period records what the row's data means — the period a price applied for, or an assignment ran
    /// for — and the values are the application's to set. A table can carry both.
    /// </para>
    /// <para>
    /// The <c>SYSTEM_TIME</c> period is deliberately NOT represented here. MariaDB lists it in
    /// <c>INFORMATION_SCHEMA.PERIODS</c> alongside application periods on an explicitly-versioned table,
    /// but it is described by <see cref="MariaDbTable.IsSystemVersioned"/> and its columns are excluded
    /// from extraction — surfacing it as a period as well would have a package declare the same state
    /// twice, in two shapes that could then disagree.
    /// </para>
    /// </summary>
    public class TablePeriod
    {
        /// <summary>The period's name, as used by <c>FOR PORTION OF &lt;name&gt;</c>.</summary>
        [JsonProperty(Order = 1)]
        public string Name { get; set; }

        /// <summary>The column holding the start of the interval, inclusive.</summary>
        [JsonProperty(Order = 2)]
        public string StartColumn { get; set; }

        /// <summary>The column holding the end of the interval, exclusive.</summary>
        [JsonProperty(Order = 3)]
        public string EndColumn { get; set; }
    }
}
