// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.ComponentModel;
using Newtonsoft.Json;

namespace Schema.Domain.MySQL
{
    public class MySqlColumn : Column
    {
        [JsonProperty(Order = 100)]
        public string GenerationExpression { get; set; }

        [JsonProperty(Order = 101)]
        public bool AutoIncrement { get; set; }

        [SchemaProperty(Pattern = "STORED|VIRTUAL")]
        [JsonProperty(Order = 102)]
        public string Generated { get; set; }

        [JsonProperty(Order = 103)]
        public string CharacterSet { get; set; }

        [JsonProperty(Order = 104)]
        public string Collation { get; set; }

        [JsonProperty(Order = 105)]
        public string Comment { get; set; }

        [JsonProperty(Order = 106)]
        public string CheckExpression { get; set; }

        // Mirrors MySqlIndex.Visible one level down (index -> column): hides the column from
        // SELECT * / INSERT-without-column-list. MySQL 8.0.23 / MariaDB 10.3 — see
        // SchemaSmith_SupportsInvisibleColumn.
        [JsonProperty(Order = 107)]
        [DefaultValue(false)]
        public bool Invisible { get; set; }

        // Restricts a spatial column to one spatial reference system (`col POINT SRID 4326`); NULL
        // means unrestricted. MySQL 8.0.3+ only -- MariaDB has no equivalent attribute at any version.
        // See SchemaSmith_SupportsColumnSrid.
        [JsonProperty(Order = 108)]
        public int? Srid { get; set; }
    }
}
