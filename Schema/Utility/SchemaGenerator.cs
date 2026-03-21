using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain;

namespace Schema.Utility;

public static class SchemaGenerator
{
    public static JObject GenerateSchema(Type rootType)
    {
        return BuildObjectSchema(rootType);
    }

    private static JObject BuildObjectSchema(Type type)
    {
        var schema = new JObject { ["type"] = "object" };
        var properties = new JObject();
        var required = new JArray();

        foreach (var prop in GetSortedProperties(type))
        {
            var propSchema = MapType(prop.PropertyType);
            ApplyConstraints(prop, propSchema);
            properties[GetPropertyName(prop)] = propSchema;

            var schemaAttr = prop.GetCustomAttribute<SchemaPropertyAttribute>();
            if (schemaAttr is { Required: true })
                required.Add(GetPropertyName(prop));
        }

        schema["properties"] = properties;
        schema["additionalProperties"] = false;
        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    private static JObject MapType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string)) return new JObject { ["type"] = "string" };
        if (type == typeof(bool)) return new JObject { ["type"] = "boolean" };
        if (IsIntegerType(type)) return new JObject { ["type"] = "integer" };
        if (IsNumberType(type)) return new JObject { ["type"] = "number" };
        if (type.IsEnum) return new JObject { ["type"] = "integer" };
        if (IsListType(type))
        {
            var elementType = type.GetGenericArguments()[0];
            var items = elementType == typeof(string) || IsIntegerType(elementType) || IsNumberType(elementType) || elementType == typeof(bool)
                ? MapType(elementType)
                : BuildObjectSchema(elementType);
            return new JObject { ["type"] = "array", ["items"] = items };
        }
        if (IsDictionaryType(type)) return new JObject { ["type"] = "object" };

        return BuildObjectSchema(type);
    }

    private static void ApplyConstraints(PropertyInfo prop, JObject propSchema)
    {
        var attr = prop.GetCustomAttribute<SchemaPropertyAttribute>();
        if (attr == null) return;
        if (!string.IsNullOrEmpty(attr.Pattern)) propSchema["pattern"] = attr.Pattern;
        if (!double.IsNaN(attr.Minimum)) propSchema["minimum"] = attr.Minimum;
        if (!double.IsNaN(attr.Maximum)) propSchema["maximum"] = attr.Maximum;
    }

    private static IEnumerable<PropertyInfo> GetSortedProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .OrderBy(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.Order ?? int.MaxValue)
            .ThenBy(p => p.Name);
    }

    private static string GetPropertyName(PropertyInfo prop)
    {
        return prop.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? prop.Name;
    }

    // Note: Enum with [JsonConverter(typeof(StringEnumConverter))] -> string+pattern is in the design
    // but deferred from this implementation — no Community domain types currently use StringEnumConverter.
    // Add when needed (e.g., if a future domain property uses a string-serialized enum).

    private static bool IsIntegerType(Type t) => t == typeof(byte) || t == typeof(short) || t == typeof(ushort) || t == typeof(int) || t == typeof(long);
    private static bool IsNumberType(Type t) => t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    private static bool IsListType(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
    private static bool IsDictionaryType(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
}
