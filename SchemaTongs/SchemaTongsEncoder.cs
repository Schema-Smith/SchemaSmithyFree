// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Utility;

namespace SchemaTongs;

internal static class SchemaTongsEncoder
{
    internal static string EncodeFileName(string schema, string name, string extension)
    {
        return $"{FileNameEncoder.Encode(schema)}.{FileNameEncoder.Encode(name)}{extension}";
    }

    internal static string EncodeFileName(string name, string extension)
    {
        return $"{FileNameEncoder.Encode(name)}{extension}";
    }

    internal static string EncodeTriggerFileName(string tableSchema, string tableName, string triggerName, string extension)
    {
        return $"{FileNameEncoder.Encode(tableSchema)}.{FileNameEncoder.Encode(tableName)}.{FileNameEncoder.Encode(triggerName)}{extension}";
    }
}
