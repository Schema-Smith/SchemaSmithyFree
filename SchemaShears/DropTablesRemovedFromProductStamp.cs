// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SchemaShears;

public static class DropTablesRemovedFromProductStamp
{
    public static void Apply(string productJsonPath)
    {
        if (!File.Exists(productJsonPath))
            throw new PatchBuildException($"Product.json not found in patch output: '{productJsonPath}'.");

        var json = JObject.Parse(File.ReadAllText(productJsonPath));
        json["DropTablesRemovedFromProduct"] = false;
        File.WriteAllText(productJsonPath, json.ToString(Formatting.Indented));
    }
}
