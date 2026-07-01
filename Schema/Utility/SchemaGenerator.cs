// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Isolators;

namespace Schema.Utility;

public static class SchemaGenerator
{
    public static JObject GenerateSchema(Type rootType)
    {
        // Identity resolver preserves the historical behavior exactly: list element types are
        // built as their declared (base) type. Existing callers/tests rely on this.
        return GenerateSchema(rootType, t => t);
    }

    /// <summary>
    /// Generates a JSON schema, mapping list element types through <paramref name="elementTypeResolver"/>
    /// so platform subclass properties appear in generated collection element schemas.
    /// </summary>
    public static JObject GenerateSchema(Type rootType, Func<Type, Type> elementTypeResolver)
    {
        return BuildObjectSchema(rootType, elementTypeResolver);
    }

    public static JObject MergeExtensionsDefinition(JObject generated, JObject existing)
    {
        if (existing == null) return generated;

        // Carry over a hand-authored Extensions fragment wherever the user defined one — table root,
        // every collection element (columns, indexes, FKs, checks, statistics, ...), single-object
        // components, and both branches of a SingleOrArray oneOf. Walk both trees in lockstep by
        // structure so a fragment is only preserved at the location it was authored.
        MergeExtensionsNode(generated, existing);
        return generated;
    }

    private static void MergeExtensionsNode(JToken generated, JToken existing)
    {
        if (generated is not JObject genObj || existing is not JObject exObj) return;

        if (genObj["properties"] is JObject genProps && exObj["properties"] is JObject exProps)
        {
            // Preserve the authored fragment at this level (only where the current model still has the slot).
            if (genProps["Extensions"] != null && exProps["Extensions"] is { } exExtensions)
                genProps["Extensions"] = exExtensions.DeepClone();

            // Recurse into every sibling property the two trees share.
            foreach (var genProp in genProps.Properties())
            {
                if (genProp.Name == "Extensions") continue;
                if (exProps[genProp.Name] is { } exChild)
                    MergeExtensionsNode(genProp.Value, exChild);
            }
        }

        // Descend through array element schemas and each SingleOrArray oneOf branch.
        if (genObj["items"] is { } genItems && exObj["items"] is { } exItems)
            MergeExtensionsNode(genItems, exItems);

        if (genObj["oneOf"] is JArray genOneOf && exObj["oneOf"] is JArray exOneOf)
            for (var i = 0; i < genOneOf.Count && i < exOneOf.Count; i++)
                MergeExtensionsNode(genOneOf[i], exOneOf[i]);
    }

    private static JObject BuildObjectSchema(Type type, Func<Type, Type> elementTypeResolver)
    {
        var schema = new JObject { ["type"] = "object" };
        var properties = new JObject();
        var required = new JArray();

        foreach (var prop in GetSortedProperties(type))
        {
            var propSchema = MapType(prop.PropertyType, elementTypeResolver);
            ApplyConstraints(prop, propSchema);

            var schemaAttr = prop.GetCustomAttribute<SchemaPropertyAttribute>();
            if (schemaAttr is { SingleOrArray: true } && propSchema["items"] is JObject itemSchema)
                propSchema = new JObject { ["oneOf"] = new JArray(itemSchema.DeepClone(), propSchema) };

            properties[GetPropertyName(prop)] = propSchema;

            if (schemaAttr is { Required: true })
                required.Add(GetPropertyName(prop));
        }

        schema["properties"] = properties;
        schema["additionalProperties"] = false;
        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    private static JObject MapType(Type type, Func<Type, Type> elementTypeResolver)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string)) return new JObject { ["type"] = "string" };
        if (type == typeof(bool)) return new JObject { ["type"] = "boolean" };
        if (IsIntegerType(type)) return new JObject { ["type"] = "integer" };
        if (IsNumberType(type)) return new JObject { ["type"] = "number" };
        if (type.IsEnum)
        {
            if (type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType == typeof(Newtonsoft.Json.Converters.StringEnumConverter))
            {
                var values = string.Join("|", Enum.GetNames(type));
                return new JObject { ["type"] = "string", ["pattern"] = values };
            }
            return new JObject { ["type"] = "integer" };
        }
        if (IsListType(type))
        {
            var elementType = type.GetGenericArguments()[0];
            // Resolve only list element types — this is the precise gap the overload closes.
            elementType = elementTypeResolver(elementType);
            var items = elementType == typeof(string) || IsIntegerType(elementType) || IsNumberType(elementType) || elementType == typeof(bool)
                ? MapType(elementType, elementTypeResolver)
                : BuildObjectSchema(elementType, elementTypeResolver);
            return new JObject { ["type"] = "array", ["items"] = items };
        }
        if (IsDictionaryType(type)) return new JObject { ["type"] = "object" };
        if (typeof(JToken).IsAssignableFrom(type)) return new JObject();

        return BuildObjectSchema(type, elementTypeResolver);
    }

    private static void ApplyConstraints(PropertyInfo prop, JObject propSchema)
    {
        var attr = prop.GetCustomAttribute<SchemaPropertyAttribute>();
        if (attr == null) return;
        if (!string.IsNullOrEmpty(attr.Pattern)) propSchema["pattern"] = attr.Pattern;
        if (!double.IsNaN(attr.Minimum)) propSchema["minimum"] = attr.Minimum;
        if (!double.IsNaN(attr.Maximum)) propSchema["maximum"] = attr.Maximum;
        if (attr.MaxLength >= 0) propSchema["maxLength"] = attr.MaxLength;
        if (!string.IsNullOrEmpty(attr.Description)) propSchema["description"] = attr.Description;
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

    private static bool IsIntegerType(Type t) => t == typeof(byte) || t == typeof(short) || t == typeof(ushort) || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong);
    private static bool IsNumberType(Type t) => t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    private static bool IsListType(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
    private static bool IsDictionaryType(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
}
