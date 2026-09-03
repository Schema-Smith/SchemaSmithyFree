// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Schema.Domain.PostgreSQL
{
    public class PostgreSqlIndex : Index
    {
        [JsonProperty(Order = 100)]
        public string FilterExpression { get; set; }

        [JsonProperty(Order = 101)]
        public bool Clustered { get; set; }

        [JsonProperty(Order = 102)]
        public string IncludeColumns { get; set; }

        [JsonProperty(Order = 103)]
        public string AccessMethod { get; set; }

        [SchemaProperty(Minimum = 0, Maximum = 100)]
        [JsonProperty(Order = 104)]
        public int FillFactor { get; set; }

        [JsonProperty(Order = 105)]
        public bool NullsNotDistinct { get; set; }

        [JsonProperty(Order = 106)]
        public bool Deferrable { get; set; }

        [JsonProperty(Order = 107)]
        public bool InitiallyDeferred { get; set; }

        [JsonProperty(Order = 108)]
        [SchemaProperty(AuthoredOnly = true)]
        public bool UpdateFillFactor { get; set; }

        // An index does NOT inherit its table's tablespace -- created with no clause it follows
        // default_tablespace, which is usually but not always the same place. Same unset-means-unmanaged
        // and create-time-only posture as PostgreSqlTable.Tablespace; see the comment there.
        [SchemaProperty(MaxLength = 128,
            Description = "PostgreSQL only. The tablespace this index lives on. Omit to leave placement unmanaged — an omitted value does NOT mean the database default, and an index does not automatically follow its table. Create-time only: moving an existing index rebuilds it, so a declared tablespace that differs from where the index already lives is refused rather than moved.")]
        [JsonProperty(Order = 113, NullValueHandling = NullValueHandling.Ignore)]
        public string Tablespace { get; set; }

        /// <summary>
        /// Per-access-method index storage parameters — the <c>WITH (...)</c> clause. This is what makes a
        /// vector index expressive: hnsw's <c>m</c> and <c>ef_construction</c>, ivfflat's <c>lists</c>,
        /// brin's <c>pages_per_range</c>, gin's <c>fastupdate</c>, and so on. Kept as an open key/value map
        /// because PostgreSQL validates each option against the chosen access method at CREATE time — a map
        /// covers every method and every future one, and the engine's own error surfaces an option a method
        /// does not accept, rather than SchemaSmith maintaining a per-method allow-list that would rot.
        /// <para><b>Deliberately excludes <c>fillfactor</c></b>, which <see cref="FillFactor"/> owns:
        /// <c>reloptions</c> stores them together, so extraction partitions fillfactor out to that property
        /// and everything else here, and neither manages the other's key.</para>
        /// <para><b>A change drops and recreates the index.</b> Several of these are build-time only — hnsw
        /// <c>m</c> and ivfflat <c>lists</c> cannot be altered in place at all — so rather than guess per
        /// option which can take an <c>ALTER INDEX ... SET</c>, a differing set rebuilds, which always
        /// works. Comparison sorts by key first, because <c>reloptions</c> reorders itself.</para>
        /// </summary>
        [JsonProperty(Order = 114, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> StorageParameters { get; set; }
    }
}
