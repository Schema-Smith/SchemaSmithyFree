// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace Schema.Isolators;

public interface IEnvironment
{
    void Exit(int exitCode);

    string CommandLine { get; }

    string GetEnvironmentVariable(string variable);
    string GetFolderPath(Environment.SpecialFolder folder);
}
