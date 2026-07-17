// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// Detects a target server's version over an open command. Detection failure is a hard
    /// error: SchemaSmith never generates or deploys against an unknown target version.
    /// </summary>
    public static class TargetVersionDetector
    {
        public static string GetVersionQuery(Platform platform) => platform switch
        {
            Platform.SqlServer => "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'))",
            Platform.PostgreSQL => "SELECT current_setting('server_version_num')",
            Platform.MySQL => "SELECT VERSION()",
            Platform.MariaDb => "SELECT VERSION()",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform for version detection")
        };

        public static TargetVersionInfo Detect(IDbCommand command, Platform platform)
        {
            command.CommandText = GetVersionQuery(platform);
            var raw = command.ExecuteScalar()?.ToString();

            // MySQL and MariaDB share the VERSION() query and comparable encoding, so a package
            // pointed at the wrong one would silently mis-generate DDL. The -MariaDB marker in the
            // version string disambiguates; fail closed on a mismatch.
            var isMariaDb = raw != null && raw.IndexOf("MariaDB", StringComparison.OrdinalIgnoreCase) >= 0;
            if (platform == Platform.MariaDb && !isMariaDb)
                throw new Exception($"Target declared as MariaDb but the server does not appear to be MariaDB (VERSION() = '{raw ?? "<null>"}').");
            if (platform == Platform.MySQL && isMariaDb)
                throw new Exception($"Target declared as MySQL but the server appears to be MariaDB (VERSION() = '{raw}'). Set Platform to MariaDb.");

            var comparable = VersionHelper.ParseDetectedVersion(raw, platform);
            if (comparable == null)
                throw new Exception($"Unable to determine the {platform} server version (got '{raw ?? "<null>"}').");
            return new TargetVersionInfo(platform, raw, comparable.Value);
        }
    }
}
