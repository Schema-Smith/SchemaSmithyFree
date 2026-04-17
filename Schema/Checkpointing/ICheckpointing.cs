// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace Schema.Checkpointing;

/// <summary>
/// Step-tracking for resumable deployments. Records completion to disk and skips
/// work that has been completed in a prior run for the same scope.
/// </summary>
public interface ICheckpointing
{
    /// <summary>
    /// Run a major deployment step under tracking. Skips work() if the step has been
    /// completed in a prior run for the same scope, and records completion after
    /// work() returns successfully.
    /// </summary>
    void Track(TrackingScope scope, string stepName, Action work);

    /// <summary>
    /// Run a script execution under tracking at script-level granularity. The slot
    /// identifies which script slot is running, and scriptPath is the unique identifier
    /// for this script within the slot.
    /// </summary>
    void TrackScript(TrackingScope scope, string slot, string scriptPath, Action work);
}
