// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace Schema.Utility
{
    /// <summary>The wire encoding used to hand the parsed model to SQL Server and to read it back.</summary>
    public enum IngestEncoding
    {
        /// <summary>JSON / OPENJSON / FOR JSON. Requires compatibility level 130+ (SQL Server 2016).</summary>
        Json,

        /// <summary>XML / .nodes()/.value() / FOR XML PATH. The backward-compatible encoding, down to SQL Server 2008.</summary>
        Xml
    }

    /// <summary>
    /// Picks the SQL Server ingest/compare encoding from the detected target and the Target:CompatEncoding
    /// override. OPENJSON's JSON-path is a parse error below compatibility level 130, independent of the
    /// server binary, so a modern server hosting a low-compat database still needs the XML encoding.
    /// </summary>
    public static class CompatEncoding
    {
        // The compatibility level at and above which OPENJSON / FOR JSON parse: SQL Server 2016.
        private const int JsonCompatFloor = 130;

        // The server major version at and above which the JSON functions exist on the binary: SQL Server 2016.
        private const int JsonServerMajorFloor = 13;

        /// <summary>
        /// Resolves the encoding. An explicit override ("legacy"/"modern", case-insensitive) wins; otherwise
        /// ("auto", the default) XML is chosen when the detected compatibility level is below 130 or the server
        /// major is below 13 (JSON functions absent on the binary), else JSON.
        /// </summary>
        public static IngestEncoding Select(string overrideValue, int? compatibilityLevel, int serverMajor)
        {
            if (string.Equals(overrideValue, "legacy", StringComparison.OrdinalIgnoreCase))
                return IngestEncoding.Xml;
            if (string.Equals(overrideValue, "modern", StringComparison.OrdinalIgnoreCase))
                return IngestEncoding.Json;

            var compatBelowFloor = compatibilityLevel is { } compat && compat < JsonCompatFloor;
            return compatBelowFloor || serverMajor < JsonServerMajorFloor
                ? IngestEncoding.Xml
                : IngestEncoding.Json;
        }
    }
}
