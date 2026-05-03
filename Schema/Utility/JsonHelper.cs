// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Newtonsoft.Json;
using Schema.Domain;
using Schema.Isolators;

namespace Schema.Utility;

public static class JsonHelper
{
    private static readonly JsonSerializerSettings SerializeSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.Indented
    };

    /// <summary>
    /// Settings that include default values (e.g., false for bools).
    /// Used when JSON consumers need all properties explicitly present (e.g., MySQL JSON_TABLE).
    /// </summary>
    private static readonly JsonSerializerSettings SerializeAllSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented
    };

    public static T Load<T>(string filePath)
    {
        if (!FileWrapper.GetFromFactory().Exists(filePath))
            throw new Exception($"File {filePath} does not exist");

        var text = FileWrapper.GetFromFactory().ReadAllText(filePath);
        return JsonConvert.DeserializeObject<T>(text);
    }

    public static Product ProductLoad(string filePath)
    {
        if (!ProductFileWrapper.GetFromFactory().Exists(filePath))
            throw new Exception($"File {filePath} does not exist");

        var text = ProductFileWrapper.GetFromFactory().ReadAllText(filePath);
        return JsonConvert.DeserializeObject<Product>(text)
            ?? throw new JsonSerializationException($"Failed to deserialize Product from {filePath}");
    }

    public static Table TableLoad(string filePath, Platform platform)
    {
        if (!ProductFileWrapper.GetFromFactory().Exists(filePath))
            throw new Exception($"File {filePath} does not exist");

        var text = ProductFileWrapper.GetFromFactory().ReadAllText(filePath);
        return PlatformDeserializer.DeserializeTable(text, platform);
    }

    public static Template TemplateLoad(string filePath, Platform platform)
    {
        if (!ProductFileWrapper.GetFromFactory().Exists(filePath))
            throw new Exception($"File {filePath} does not exist");

        var text = ProductFileWrapper.GetFromFactory().ReadAllText(filePath);
        return PlatformDeserializer.DeserializeTemplate(text, platform);
    }

    public static string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, SerializeSettings);
    }

    /// <summary>
    /// Serializes including default-valued properties (e.g., bool=false).
    /// Required for MySQL JSON_TABLE which needs explicit property presence.
    /// </summary>
    public static string SerializeAll<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, SerializeAllSettings);
    }

    public static void Write<T>(string filePath, T obj)
    {
        FileWrapper.GetFromFactory().WriteAllText(filePath, Serialize(obj));
    }
}
