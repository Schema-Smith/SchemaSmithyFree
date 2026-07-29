// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// Enforces SchemaSmith's intrinsic per-engine version floor — and, for SQL Server, the target
    /// database's compatibility level — before any kindling, deployment, or extraction. This is
    /// distinct from the opt-in <c>Product.MinimumVersion</c> guardrail: the floor is structural, the
    /// version below which the engine scripts do not run at all. The SQL Server server binary must be
    /// 2017+ (<c>STRING_AGG</c>), and the target database's compatibility level must be 130+
    /// (<c>OPENJSON</c>'s JSON-path is a parse error below compat 130). Failing here produces a clear
    /// "unsupported version" message instead of a raw engine error mid-run.
    /// </summary>
    public static class PreFlightVersionGuard
    {
        // OPENJSON (the JSON ingest) is compatibility-level-gated at 130, independent of the server
        // binary — a 2017+ server hosting a database left at compat 120 fails to parse it. STRING_AGG
        // is server-version-gated (2017), not compat-gated, so it runs at compat 130. Both verified end
        // to end at compat 130 on a 2019 server (floor-lowering spike 2026-07-27); 120 fails on OPENJSON.
        private const int SqlServerCompatFloor = 130;

        /// <summary>
        /// Throws when <paramref name="info"/> is below the engine floor, or (SQL Server) when its
        /// detected database compatibility level is below 130. <paramref name="databaseLabel"/> names
        /// the database in the compatibility-level message.
        /// </summary>
        public static void CheckOrThrow(TargetVersionInfo info, string serverLabel, string databaseLabel = null)
        {
            if (VersionHelper.IsBelowFloor(info.Platform, info.ServerComparable))
                throw new Exception(
                    $"{serverLabel}: detected {info.Platform} version {VersionHelper.DisplayVersion(info)} is below the minimum " +
                    $"supported version {VersionHelper.HardFloorDisplay(info.Platform)}. SchemaSmith cannot run against it.");

            if (info.Platform.GetBasePlatform() == Platform.SqlServer &&
                info.CompatibilityLevel is { } compat && compat < SqlServerCompatFloor)
                throw new Exception(
                    $"{serverLabel}: database {databaseLabel} is at compatibility level {compat}; SchemaSmith requires " +
                    $"{SqlServerCompatFloor} (SQL Server 2016) or higher. Raise it with " +
                    $"ALTER DATABASE ... SET COMPATIBILITY_LEVEL = {SqlServerCompatFloor}.");
        }
    }
}
