// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.Domain.MySQL
{
    public class MySqlTable : Table, IDeliverableTable
    {
        string IDeliverableTable.Schema => null;

        [JsonIgnore]
        public IReadOnlyList<IDeliverableColumn> DeliverableColumns => Columns.Cast<IDeliverableColumn>().ToList();

        [JsonIgnore]
        public IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys => ForeignKeys.Cast<IDeliverableForeignKey>().ToList();

        public override void ResolveScriptTokensInTableComponentScripts(List<KeyValuePair<string, string>> tokens)
        {
            base.ResolveScriptTokensInTableComponentScripts(tokens);
            var tableTokens = tokens.Concat(GetCustomTokens(Extensions, "Table.")).ToList();
            foreach (var column in Columns.OfType<MySqlColumn>())
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(column.Extensions)).ToList();
                column.GenerationExpression = TableTokenReplace(column.GenerationExpression, scriptTokens);
            }
        }

        [JsonProperty(Order = 100)]
        public string Engine { get; set; } = "InnoDB";

        [SchemaProperty(Pattern = "Dynamic|Compact|Compressed|Redundant|Fixed")]
        [JsonProperty(Order = 101)]
        public string RowFormat { get; set; }

        [JsonProperty(Order = 102)]
        public string CharacterSet { get; set; }

        [JsonProperty(Order = 103)]
        public string Collation { get; set; }

        [JsonProperty(Order = 104)]
        public string Comment { get; set; }

        [SchemaProperty(Minimum = 1)]
        [JsonProperty(Order = 105)]
        public ulong? AutoIncrementValue { get; set; }

        [JsonProperty(Order = 106)]
        public List<FullTextIndex> FullTextIndexes { get; set; } = [];

        // InnoDB transparent page compression. MariaDB has no COMPRESSION table option at all -- it spells
        // the same idea PAGE_COMPRESSED, on MariaDbTable -- so this is scoped to MySQL rather than left to
        // advertise a setting MariaDB would reject.
        [SchemaProperty(Pattern = "zlib|lz4|none", Platforms = [Platform.MySQL],
            Description = "MySQL only. InnoDB transparent page compression for the table's tablespace file. MariaDB spells this PAGE_COMPRESSED instead.")]
        [JsonProperty(Order = 107, NullValueHandling = NullValueHandling.Ignore)]
        public string Compression { get; set; }

        // The compression page size OF RowFormat = COMPRESSED, which is why it lives beside it rather than
        // as a row of its own -- it is meaningless without it, the same relationship PadIndex has to
        // FillFactor on SQL Server. Both engines report it identically (verified on 5.7, 8.0 and 10.2).
        [SchemaProperty(Minimum = 1, Maximum = 16,
            Description = "InnoDB compressed-page size in KB (1, 2, 4, 8 or 16). Only meaningful with RowFormat = COMPRESSED.")]
        [JsonProperty(Order = 108, NullValueHandling = NullValueHandling.Ignore)]
        public int? KeyBlockSize { get; set; }
    }
}
