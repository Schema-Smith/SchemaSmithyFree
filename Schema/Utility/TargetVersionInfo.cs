// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// The detected version of a target server. <see cref="ServerComparable"/> is the
    /// per-platform major-based comparable (see <see cref="VersionHelper"/>).
    /// <see cref="CompatibilityLevel"/> is the target database's <c>compatibility_level</c>
    /// (SQL Server only, and only when a database name is supplied to detection); <c>null</c>
    /// on other engines or when not detected.
    /// </summary>
    public sealed record TargetVersionInfo(Platform Platform, string RawVersion, int ServerComparable,
        int? CompatibilityLevel = null);
}
