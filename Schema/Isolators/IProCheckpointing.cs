// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using SchemaSmith.Pro;

namespace Schema.Isolators;

public interface IProCheckpointing
{
    void Track(TrackingScope scope, string stepName, Action work);
    void TrackScript(TrackingScope scope, string slot, string scriptPath, Action work);
}
