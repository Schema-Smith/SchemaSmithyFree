// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System.IO;

﻿using System.IO;

namespace Schema.Isolators;

public interface IFileSystemInfo
{
    FileAttributes Attributes { get; }
    bool Exists { get; }
    string FullName { get; }
    string Name { get; }
}
