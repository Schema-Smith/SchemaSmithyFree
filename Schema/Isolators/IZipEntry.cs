// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;

namespace Schema.Isolators;

public interface IZipEntry
{
    string FullName { get; }
    Stream Open();
}
