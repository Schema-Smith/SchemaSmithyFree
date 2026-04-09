// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// Provides version comparison utilities for MinimumVersion-based feature gating.
    /// MinimumVersion is a string property on Product that declares the highest feature level
    /// the product is designed to support (acts as a ceiling).
    /// </summary>
    public static class VersionHelper
    {
        /// <summary>
        /// Checks whether the given MinimumVersion string meets or exceeds the required version
        /// for a given platform. Returns true if the feature should be available.
        /// </summary>
        /// <param name="minimumVersion">The product's MinimumVersion string (e.g., "2019", "15", "8.0"). Null/empty means no ceiling.</param>
        /// <param name="requiredVersion">The version threshold for the feature (e.g., "15" for PG MERGE).</param>
        /// <param name="platform">The target platform, used to select the parsing strategy.</param>
        /// <returns>True if minimumVersion >= requiredVersion, or if minimumVersion is null/empty (no ceiling).</returns>
        public static bool MeetsVersionThreshold(string minimumVersion, string requiredVersion, Platform platform)
        {
            // No MinimumVersion set means no ceiling — all features available
            if (string.IsNullOrWhiteSpace(minimumVersion))
                return true;

            var minimum = ParseVersion(minimumVersion, platform);
            var required = ParseVersion(requiredVersion, platform);

            if (minimum == null || required == null)
                return true; // Unparseable versions default to allowing features

            return minimum >= required;
        }

        /// <summary>
        /// Parses a version string into a comparable integer value.
        /// SQL Server: "2017", "2019", "2022" — plain year numbers.
        /// PostgreSQL: "15", "16" — major version numbers.
        /// MySQL: "8.0", "8.4", "9.0" — major.minor format, normalized to major * 100 + minor.
        /// </summary>
        internal static int? ParseVersion(string version, Platform platform)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            version = version.Trim();

            return platform switch
            {
                Platform.MySQL => ParseMySqlVersion(version),
                _ => ParseSimpleVersion(version)
            };
        }

        /// <summary>
        /// Parses MySQL version strings like "8.0", "8.4", "9.0" into comparable integers.
        /// Falls back to simple integer parsing for plain numbers like "8".
        /// </summary>
        private static int? ParseMySqlVersion(string version)
        {
            if (version.Contains('.'))
            {
                var parts = version.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
                    return major * 100 + minor;
            }

            // Fall back to simple parse
            return int.TryParse(version, out var simple) ? simple * 100 : null;
        }

        /// <summary>
        /// Parses simple integer version strings (SQL Server years like "2019", PG majors like "15").
        /// Also handles dotted versions by taking just the first component.
        /// </summary>
        private static int? ParseSimpleVersion(string version)
        {
            // Handle dotted versions by taking first component
            if (version.Contains('.'))
            {
                var parts = version.Split('.');
                version = parts[0];
            }

            return int.TryParse(version, out var result) ? result : null;
        }
    }
}
