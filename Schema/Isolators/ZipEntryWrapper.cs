// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.IO.Compression;

namespace Schema.Isolators;

public class ZipEntryWrapper : IZipEntry
{
    private readonly ZipArchiveEntry _entry;

    public ZipEntryWrapper(ZipArchiveEntry entry)
    {
        _entry = entry;
    }

    public string FullName => _entry.FullName;
    public Stream Open() => _entry.Open();
}
