// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Platform
    {
        Unknown,
        PostgreSQL,
        SqlServer,
        MySQL,
        MariaDb
        // Future: Oracle, AzureSql, AzurePostgreSql
    }

    public static class PlatformExtensions
    {
        public static Platform GetBasePlatform(this Platform platform) => platform switch
        {
            Platform.Unknown => throw new ArgumentException("Platform has not been assigned.", nameof(platform)),
            Platform.MariaDb => Platform.MySQL,
            // Future variants map to base:
            // Platform.AzureSql => Platform.SqlServer,
            // Platform.AzurePostgreSql => Platform.PostgreSQL,
            _ => platform
        };

        public static Platform ParsePlatform(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Platform value cannot be empty.", nameof(value));

            if (value.Equals("MSSQL", StringComparison.OrdinalIgnoreCase)
                || value.Equals("SQL Server", StringComparison.OrdinalIgnoreCase))
                return Platform.SqlServer;

            if (Enum.TryParse<Platform>(value, ignoreCase: true, out var platform) && platform != Platform.Unknown)
                return platform;

            throw new ArgumentException(
                $"Unknown platform '{value}'. Supported platforms: SqlServer (or MSSQL), PostgreSQL, MySQL, MariaDb.",
                nameof(value));
        }

        public static string ToCanonicalString(this Platform platform) => platform.ToString();

        /// <summary>
        /// The engine name as a person writes it, for text a human reads -- schema descriptions, docs,
        /// messages. Distinct from <see cref="ToCanonicalString"/>, which is the identifier form used in
        /// file names and configuration. ParsePlatform already accepts these spellings as input aliases.
        /// </summary>
        public static string ToDisplayName(this Platform platform) => platform switch
        {
            Platform.SqlServer => "SQL Server",
            Platform.PostgreSQL => "PostgreSQL",
            Platform.MySQL => "MySQL",
            Platform.MariaDb => "MariaDB",
            _ => platform.ToString()
        };

        public static string GetDefaultSchema(this Platform platform) => platform switch
        {
            Platform.SqlServer => "dbo",
            Platform.PostgreSQL => "public",
            Platform.MySQL => "",
            Platform.MariaDb => "",
            _ => throw new ArgumentException($"Platform '{platform}' does not have a default schema.", nameof(platform))
        };
    }
}
