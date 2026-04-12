// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Domain;

public abstract class DynamicBase
{
    [JsonProperty(Order = 999)]
    public JToken Extensions { get; set; }

    public object GetExtensionProperty(string name)
    {
        return (Extensions as JObject)?[name]?.ToObject<object>();
    }

    public void SetExtensionProperty(string name, object value)
    {
        if (Extensions == null) Extensions = new JObject();
        ((JObject)Extensions)[name] = value == null ? null : JToken.FromObject(value);
    }
}
