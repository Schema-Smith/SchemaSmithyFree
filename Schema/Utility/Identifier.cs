// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;

namespace Schema.Utility;

/// <summary>
/// Platform-aware identifier helpers. Today this is a single delimiter-stripping
/// method used for same-schema identifier comparison in schema-template scrubbing
/// (issue #256), but the helper is also the right home for any future
/// extraction/data-delivery/scrub site that needs identifiers compared as
/// equivalent regardless of how either side was delimited.
/// </summary>
public static class Identifier
{
    /// <summary>
    /// Strips one layer of platform-appropriate delimiter wrapping from
    /// <paramref name="value"/>. Bracket-quoted SQL Server identifiers (<c>[name]</c>),
    /// double-quoted PostgreSQL identifiers (<c>"name"</c>), and backtick-quoted MySQL
    /// identifiers (<c>`name`</c>) are unwrapped; embedded delimiter escape sequences
    /// (<c>]]</c>, <c>""</c>, <c>``</c>) are collapsed.
    /// </summary>
    public static string Unwrap(string value, Platform platform)
    {
        if (string.IsNullOrEmpty(value)) return value;
        switch (platform)
        {
            case Platform.SqlServer:
                if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
                    return value.Substring(1, value.Length - 2).Replace("]]", "]");
                return value;
            case Platform.PostgreSQL:
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                    return value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
                return value;
            case Platform.MySQL:
                return MySqlReservedWords.Unquote(value);
            default:
                return value;
        }
    }
}
