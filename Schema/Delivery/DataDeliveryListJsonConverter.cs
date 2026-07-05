// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Delivery;

// A table historically carried ONE data delivery; an array here declares independently-gated
// deliveries — environment variants (mutually exclusive gates) or additive patch slices — each
// selected per target by its ShouldApplyExpression. A bare object is the back-compat single-delivery form.
public class DataDeliveryListJsonConverter : JsonConverter<List<DataDelivery>>
{
    public override List<DataDelivery> ReadJson(JsonReader reader, Type objectType,
        List<DataDelivery> existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return [];
        var token = JToken.Load(reader);
        var deliveries = token.Type == JTokenType.Array
            ? token.ToObject<List<DataDelivery>>(serializer) ?? []
            : [token.ToObject<DataDelivery>(serializer)];

        if (deliveries.Any(d => d == null))
            throw new JsonSerializationException("DataDelivery entries cannot be null.");

        if (deliveries.Count > 1 && deliveries.Any(d => string.IsNullOrWhiteSpace(d.ShouldApplyExpression)))
            throw new JsonSerializationException(
                "Multiple DataDelivery entries require a ShouldApplyExpression on every entry. " +
                "A single ungated delivery is the bare-object form; multiple deliveries are gated variants or patch slices.");

        return deliveries;
    }

    public override void WriteJson(JsonWriter writer, List<DataDelivery> value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }
        if (value.Count == 1)
            serializer.Serialize(writer, value[0]);
        else
            serializer.Serialize(writer, value.ToArray());
    }
}
