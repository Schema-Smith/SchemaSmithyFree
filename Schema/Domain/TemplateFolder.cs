// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain
{
    public class TemplateFolder : ScriptFolder
    {
        [JsonProperty(Order = 2)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TemplateQuenchSlot QuenchSlot { get; set; }

        public TemplateFolder Clone()
        {
            var clone = new TemplateFolder { QuenchSlot = QuenchSlot };
            DeepClone(clone);
            return clone;
        }
    }
}
