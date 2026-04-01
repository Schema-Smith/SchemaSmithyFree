// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System;

namespace Schema.Isolators;

public class EnvironmentWrapper : IEnvironment
{
    public void Exit(int exitCode)
    {
        Environment.Exit(exitCode);
    }

    public string CommandLine { get; } = Environment.CommandLine;

    public static IEnvironment GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IEnvironment, EnvironmentWrapper>();
    }
}
