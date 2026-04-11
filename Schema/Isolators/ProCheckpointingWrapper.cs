// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using SchemaSmith.Pro;

namespace Schema.Isolators;

public class ProCheckpointingWrapper : IProCheckpointing
{
    public void Track(TrackingScope scope, string stepName, Action work)
        => ProServices.Checkpointing.Track(scope, stepName, work);

    public void TrackScript(TrackingScope scope, string slot, string scriptPath, Action work)
        => ProServices.Checkpointing.TrackScript(scope, slot, scriptPath, work);

    public static IProCheckpointing GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IProCheckpointing, ProCheckpointingWrapper>();
}
