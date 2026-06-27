// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// The detected version of a target server. <see cref="ServerComparable"/> is the
    /// per-platform major-based comparable (see <see cref="VersionHelper"/>). Per-database
    /// compatibility_level joins this record when the code-generation slices need it.
    /// </summary>
    public sealed record TargetVersionInfo(Platform Platform, string RawVersion, int ServerComparable);
}
