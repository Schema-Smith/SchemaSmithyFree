// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Schema.Utility;

public static class InternalExtendedProperties
{
    public static readonly HashSet<string> Names =
        new(StringComparer.OrdinalIgnoreCase) { "ProductName" };

    public static bool IsInternal(string name) => Names.Contains(name);

    public static string SqlExclusionFilter =>
        string.Join(",", Names.Select(n => $"N'{n}'"));
}
