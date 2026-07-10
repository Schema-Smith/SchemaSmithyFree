// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Utility;

/// <summary>
/// Derives the canonical on-disk file name for a table from its content identity
/// (<c>schema</c> + <c>name</c> + optional <c>variantName</c>). The filename is a validated
/// convention, never the source of identity — variants of one table sort together because the
/// variant label is appended after the schema and table segments. Blank variant reproduces the
/// historical <c>&lt;schema&gt;.&lt;table&gt;.json</c> name, so existing packages are unchanged.
/// </summary>
public static class TableFileName
{
    public static string Canonical(string schema, string name, string variantName, bool isSchemaTemplate)
    {
        var body = isSchemaTemplate
            ? FileNameEncoder.Encode(name)
            : $"{FileNameEncoder.Encode(schema)}.{FileNameEncoder.Encode(name)}";
        var variantSuffix = string.IsNullOrWhiteSpace(variantName)
            ? ""
            : $".{FileNameEncoder.Encode(variantName.Trim())}";
        return $"{body}{variantSuffix}.json";
    }
}
