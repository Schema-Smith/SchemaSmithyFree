// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Utility;

public static class StringHelper
{
    public static string StripBrackets(string name)
    {
        return name?.Replace("[", "").Replace("]", "") ?? "";
    }
}
