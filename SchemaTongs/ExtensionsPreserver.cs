// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Utility;

namespace SchemaTongs;

public static class ExtensionsPreserver
{
    public static void PreserveTableExtensions(Table original, Table extracted)
    {
        extracted.Extensions = original.Extensions;

        PreserveNamedExtensions(original.Columns, extracted.Columns, c => Strip(c.Name), c => Strip(c.OldName));
        PreserveNamedExtensions(original.Indexes, extracted.Indexes, i => Strip(i.Name));
        PreserveNamedExtensions(original.ForeignKeys, extracted.ForeignKeys, f => Strip(f.Name));
        PreserveNamedExtensions(original.CheckConstraints, extracted.CheckConstraints, c => Strip(c.Name));
        PreserveNamedExtensions(original.Statistics, extracted.Statistics, s => Strip(s.Name));
        PreserveNamedExtensions(original.XmlIndexes, extracted.XmlIndexes, x => Strip(x.Name));

        if (original.FullTextIndex?.Extensions != null && extracted.FullTextIndex != null)
            extracted.FullTextIndex.Extensions = original.FullTextIndex.Extensions;
    }

    public static void PreserveIndexedViewExtensions(IndexedView original, IndexedView extracted)
    {
        extracted.Extensions = original.Extensions;
        PreserveNamedExtensions(original.Indexes, extracted.Indexes, i => Strip(i.Name));
    }

    private static void PreserveNamedExtensions<T>(List<T> originals, List<T> extracteds,
        Func<T, string> getName, Func<T, string> getOldName = null)
    {
        var extensionsProp = typeof(T).GetProperty("Extensions");
        if (extensionsProp == null) return;

        foreach (var original in originals)
        {
            var origExt = extensionsProp.GetValue(original) as JToken;
            if (origExt == null) continue;

            var origName = getName(original);
            var match = extracteds.FirstOrDefault(e => getName(e).Equals(origName, StringComparison.OrdinalIgnoreCase));

            if (match == null && getOldName != null)
            {
                match = extracteds.FirstOrDefault(e =>
                {
                    var oldName = getOldName(e);
                    return !string.IsNullOrEmpty(oldName) && Strip(oldName).Equals(origName, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (match != null)
                extensionsProp.SetValue(match, origExt);
        }
    }

    private static string Strip(string name) => StringHelper.StripBrackets(name);
}
