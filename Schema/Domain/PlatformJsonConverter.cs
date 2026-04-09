// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Newtonsoft.Json;

namespace Schema.Domain
{
    public class PlatformJsonConverter : JsonConverter<Platform>
    {
        public override Platform ReadJson(JsonReader reader, Type objectType,
            Platform existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = reader.Value as string;
            if (value == null)
                throw new JsonSerializationException("Platform value cannot be null.");

            try
            {
                return PlatformExtensions.ParsePlatform(value);
            }
            catch (ArgumentException ex)
            {
                throw new JsonSerializationException(ex.Message, ex);
            }
        }

        public override void WriteJson(JsonWriter writer, Platform value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToCanonicalString());
        }
    }
}
