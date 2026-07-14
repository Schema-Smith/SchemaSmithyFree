// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// Normalizes engine version strings to a per-platform integer comparable (major-based)
    /// and compares them. Detected target version drives code generation; this utility is the
    /// shared comparison primitive for that and for the MinimumVersion pre-flight gate.
    /// </summary>
    public static class VersionHelper
    {
        // SQL Server release-year -> major-version map (declared-version year alias).
        private static readonly Dictionary<int, int> SqlServerYearToMajor = new()
        {
            { 2016, 13 }, { 2017, 14 }, { 2019, 15 }, { 2022, 16 }
        };

        /// <summary>Normalizes a user-declared version (MinimumVersion or a feature threshold) to the comparable.</summary>
        public static int? ParseDeclaredVersion(string version, Platform platform)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            version = version.Trim();

            if (platform.GetBasePlatform() == Platform.MySQL) return ParseMajorMinor(version);

            if (!int.TryParse(SplitFirst(version), out var value)) return null;

            // SQL Server: a year (>= 2000) is an alias for its major.
            if (platform == Platform.SqlServer && value >= 2000)
                return SqlServerYearToMajor.TryGetValue(value, out var major) ? major : (int?)null;

            return value;
        }

        /// <summary>Normalizes a raw detection-query result to the comparable.</summary>
        public static int? ParseDetectedVersion(string rawVersion, Platform platform)
        {
            if (string.IsNullOrWhiteSpace(rawVersion)) return null;
            rawVersion = rawVersion.Trim();

            return platform switch
            {
                // PostgreSQL current_setting('server_version_num') -> e.g. 160004 -> major 16.
                Platform.PostgreSQL => int.TryParse(rawVersion, out var num) ? num / 10000 : (int?)null,
                // MySQL VERSION() -> e.g. "8.0.36" -> 800.
                // MariaDb VERSION() -> e.g. "10.6.27-MariaDB" -> 1006 (the -MariaDB suffix sits
                // on the patch part, which ParseMajorMinor ignores).
                Platform.MySQL or Platform.MariaDb => ParseMajorMinor(rawVersion),
                // SQL Server SERVERPROPERTY('ProductMajorVersion') -> already the major.
                _ => int.TryParse(SplitFirst(rawVersion), out var v) ? v : (int?)null
            };
        }

        /// <summary>True when the detected comparable meets or exceeds the required comparable.</summary>
        public static bool IsAtLeast(int detectedComparable, int requiredComparable)
            => detectedComparable >= requiredComparable;

        // "8.0.36" / "8.4" / "8" -> major*100 + minor.
        private static int? ParseMajorMinor(string version)
        {
            var parts = version.Split('.');
            if (!int.TryParse(parts[0], out var major)) return null;
            var minor = 0;
            if (parts.Length >= 2 && !int.TryParse(parts[1], out minor)) return null;
            return major * 100 + minor;
        }

        private static string SplitFirst(string version)
            => version.Contains('.') ? version.Split('.')[0] : version;
    }
}
