// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaShears;

public class PatchBuildRequest
{
    public string SourcePath { get; set; }
    public string ManifestPath { get; set; }
    public string AlwaysIncludePath { get; set; }
    public string OutputPath { get; set; }
    public bool Zip { get; set; }
}

public class PatchBuilder
{
    public virtual void Build(PatchBuildRequest request)
    {
        // Implemented in Task 8.
    }
}
