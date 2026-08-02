// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
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
        // SQL Server release-year -> major-version map (declared-version year alias). Bottoms out at
        // 2008 (major 10): the intrinsic floor. Below compat 130 SchemaSmith ingests/compares the model
        // as XML (the JSON cliff), so 2008-2016 are supported; the separate DB compatibility-level floor
        // is 100 — see PreFlightVersionGuard.
        private static readonly Dictionary<int, int> SqlServerYearToMajor = new()
        {
            { 2008, 10 }, { 2012, 11 }, { 2014, 12 }, { 2016, 13 }, { 2017, 14 }, { 2019, 15 }, { 2022, 16 }
        };

        // Intrinsic per-engine hard floor: the lowest version the current script set supports, matching
        // the "Supported engine floors" table in the SchemaQuench reference docs. Enforced before any
        // deployment or extraction, independent of the opt-in Product.MinimumVersion guardrail. Keyed on
        // the actual platform (MariaDB's floor differs from MySQL's despite sharing the base engine).
        private static int HardFloorComparable(Platform platform) => platform switch
        {
            Platform.SqlServer => 10,     // SQL Server 2008 (below compat 130 the model is ingested/compared as XML)
            Platform.PostgreSQL => 12,    // PostgreSQL 12 (features above 12 — per-column compression + expression statistics (14), NULLS NOT DISTINCT (15) — are degraded per the unsupported-feature policy)
            Platform.MySQL => 800,        // MySQL 8.0
            Platform.MariaDb => 1006,     // MariaDB 10.6
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "No version floor defined for platform")
        };

        /// <summary>The comparable of the intrinsic hard floor for the platform.</summary>
        public static int HardFloor(Platform platform) => HardFloorComparable(platform);

        /// <summary>Human-friendly form of the hard floor, matching the reference docs' floors table.</summary>
        public static string HardFloorDisplay(Platform platform) => platform switch
        {
            Platform.SqlServer => "2008 (major 10)",
            Platform.PostgreSQL => "12",
            Platform.MySQL => "8.0",
            Platform.MariaDb => "10.6",
            _ => HardFloorComparable(platform).ToString()
        };

        /// <summary>True when the detected server comparable is below the platform's intrinsic hard floor.</summary>
        public static bool IsBelowFloor(Platform platform, int serverComparable)
            => serverComparable < HardFloorComparable(platform);

        /// <summary>
        /// Human-friendly detected-version string for logging. PostgreSQL's raw
        /// <c>server_version_num</c> (e.g. <c>160013</c>) is normalized to its major (<c>16</c>);
        /// other engines report a readable version already.
        /// </summary>
        public static string DisplayVersion(TargetVersionInfo info) =>
            info.Platform.GetBasePlatform() == Platform.PostgreSQL ? info.ServerComparable.ToString() : info.RawVersion;

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
