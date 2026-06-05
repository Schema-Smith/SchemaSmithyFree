// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Domain.SqlServer
{
    // SQL Server allows ONE full-text index per table; an array here declares conditional
    // variants of that one index, selected per target by mutually exclusive ShouldApplyExpressions.
    public class FullTextIndexListJsonConverter : JsonConverter<List<FullTextIndex>>
    {
        public override List<FullTextIndex> ReadJson(JsonReader reader, Type objectType,
            List<FullTextIndex> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return [];
            var token = JToken.Load(reader);
            var variants = token.Type == JTokenType.Array
                ? token.ToObject<List<FullTextIndex>>(serializer) ?? []
                : [token.ToObject<FullTextIndex>(serializer)];

            if (variants.Any(v => v == null))
                throw new JsonSerializationException("FullTextIndex variant entries cannot be null.");

            if (variants.Count > 1 && variants.Any(v => string.IsNullOrWhiteSpace(v.ShouldApplyExpression)))
                throw new JsonSerializationException(
                    "Multiple FullTextIndex variants require a ShouldApplyExpression on every variant. " +
                    "SQL Server allows one full-text index per table; variants are selected by mutually exclusive expressions.");

            return variants;
        }

        public override void WriteJson(JsonWriter writer, List<FullTextIndex> value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }
            if (value.Count == 1)
                serializer.Serialize(writer, value[0]);
            else
                serializer.Serialize(writer, value.ToArray());
        }
    }
}
