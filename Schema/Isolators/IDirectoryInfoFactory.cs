// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
namespace Schema.Isolators;

﻿namespace Schema.Isolators;

public interface IDirectoryInfoFactory
{
    IDirectoryInfo GetDirectoryInfoWrapper(string path);
}