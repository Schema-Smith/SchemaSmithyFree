// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// Enforces SchemaSmith's intrinsic per-engine version floor — and, for SQL Server, the target
    /// database's compatibility level — before any kindling, deployment, or extraction. This is
    /// distinct from the opt-in <c>Product.MinimumVersion</c> guardrail: the floor is structural, the
    /// version below which the engine scripts do not run at all (for example <c>STRING_AGG</c> needs
    /// SQL Server 2017 and a database compatibility level of 140). Failing here produces a clear
    /// "unsupported version" message instead of a raw engine error mid-run.
    /// </summary>
    public static class PreFlightVersionGuard
    {
        private const int SqlServerCompatFloor = 140;   // STRING_AGG requires compatibility level 140

        /// <summary>
        /// Throws when <paramref name="info"/> is below the engine floor, or (SQL Server) when its
        /// detected database compatibility level is below 140. <paramref name="databaseLabel"/> names
        /// the database in the compatibility-level message.
        /// </summary>
        public static void CheckOrThrow(TargetVersionInfo info, string serverLabel, string databaseLabel = null)
        {
            if (VersionHelper.IsBelowFloor(info.Platform, info.ServerComparable))
                throw new Exception(
                    $"{serverLabel}: detected {info.Platform} version {info.RawVersion} is below the minimum " +
                    $"supported version {VersionHelper.HardFloorDisplay(info.Platform)}. SchemaSmith cannot run against it.");

            if (info.Platform.GetBasePlatform() == Platform.SqlServer &&
                info.CompatibilityLevel is { } compat && compat < SqlServerCompatFloor)
                throw new Exception(
                    $"{serverLabel}: database {databaseLabel} is at compatibility level {compat}; SchemaSmith requires " +
                    $"{SqlServerCompatFloor} (SQL Server 2017) or higher. Raise it with " +
                    $"ALTER DATABASE ... SET COMPATIBILITY_LEVEL = {SqlServerCompatFloor}.");
        }
    }
}
